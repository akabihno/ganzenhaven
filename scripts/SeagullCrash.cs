using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Cutscene: the player stands in the harbourmaster's office on a bright, foggy morning.
/// The broken-winged seagull labours in out of the fog, bursts through the open window arch,
/// hits the floorboards, bounces, tumbles and comes to rest at the player's feet.
/// </summary>
/// <remarks>
/// <para>Everything runs off one clock, so the shot is identical on every run, which is what
/// Movie Maker needs. With Movie Maker on, the scene quits by itself when the shot ends so
/// the file is finalised; otherwise it loops.</para>
/// <para>Two seagull models share one pivot: a rigged flying gull (wings spread, rig built by
/// tools/blender_rig_seagull.py) whose wing bones are driven here, and the standing
/// broken-winged gull it turns into at the moment it hits the floor, hidden by the dust and
/// feathers. In flight the good wing pumps hard and the broken right wing droops and flails;
/// the wings tuck up into a V to fit through the arch.</para>
/// <para>The room itself (window, shutters, materials, props, fire) is dressed by
/// <see cref="OfficeRestyle"/>.</para>
/// </remarks>
public partial class SeagullCrash : Node3D
{
	/// <summary>The shot is over (played inside the game with <see cref="Loop"/> off).</summary>
	[Signal] public delegate void FinishedEventHandler();

	/// <summary>Standing height of the seagull in metres.</summary>
	[Export] public float SeagullHeight { get; set; } = 0.5f;

	/// <summary>Wingspan of the flying gull in metres.</summary>
	[Export] public float FlyingWingspan { get; set; } = 1.35f;

	/// <summary>Meshy exports face +Z; Godot's forward is -Z.</summary>
	[Export] public float ModelYawDegrees { get; set; } = 180f;

	/// <summary>Restart the shot when it ends (ignored while recording a movie).</summary>
	[Export] public bool Loop { get; set; } = true;

	// Timeline, in seconds.
	private const float TWindow = 4.4f;          // passes through the right-hand arch
	private const float BounceTime = 0.29f;
	private const float RollTime = 1.0f;
	private const float TGetUp = 8.1f;
	private const float GetUpTime = 0.7f;
	private const float TEnd = 11.0f;
	private const float LoopPause = 1.5f;

	private const float Gravity = 13f;           // a bit above real g: reads snappier
	private const float FloorY = 0f;
	private const float RollRadius = 0.24f;

	private static readonly Vector3 WindowPoint = OfficeRestyle.RightArchCentre;
	private static readonly Vector3 FallVelocity = new(-0.35f, 0.7f, 3.4f);
	private static readonly Vector3 BounceVelocity = new(-0.1f, 1.9f, 2.1f);
	private static readonly Vector3 CameraPosition = new(0.35f, 1.62f, -0.3f);
	// Opening framing: the hearth on the left, the arches on the right.
	private static readonly Vector3 WindowCentre = new(-0.45f, 1.45f, -5.0f);

	// Arch -> first floor contact (pivot about 0.22 m off the boards), from the ballistic arc.
	private static readonly float FallTime =
		(FallVelocity.Y + Mathf.Sqrt(FallVelocity.Y * FallVelocity.Y + 2f * Gravity * (WindowPoint.Y - 0.22f))) / Gravity;

	// Out in the fog -> weaving in -> through the open window arch.
	private static readonly Vector3[] FlightPath =
	{
		new(9.5f, 5.0f, -38f),
		new(9.5f, 5.0f, -38f),
		new(4.5f, 3.8f, -26f),
		new(-2f, 3.0f, -16f),
		new(1.9f, 2.3f, -10f),
		new(0.2f, 1.9f, -7f),
		WindowPoint,
		WindowPoint + new Vector3(FallVelocity.X, 0f, FallVelocity.Z).Normalized() * 1.6f,
	};

	private Node3D _bird, _model, _flyer, _shutter;
	private Skeleton3D _wings;
	private readonly Dictionary<string, (int Bone, Quaternion RestRotation, Vector3 Axis)> _wingBones = new();
	private float _flapPhase;
	private Camera3D _camera;
	private CpuParticles3D _windowFeathers, _impactFeathers, _impactDust;
	private readonly List<Vector3> _contactPoints = new();

	private double _clock;
	private bool _windowBurstDone, _impactDone, _finished;
	private Vector3 _look = WindowCentre;
	private float _shake;
	private Basis _restBasis;
	private Vector3 _restPosition;

	public override void _Ready()
	{
		GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
		_camera = GetNode<Camera3D>("PlayerCamera");
		_camera.MakeCurrent();

		_shutter = GetNode<OfficeRestyle>("OfficeRestyle").RightShutter;
		PrepareSeagull();
		PrepareFlyer();
		_windowFeathers = MakeFeathers(new Vector3(0f, 0.1f, 1f), 70f, 0.25f);
		_impactFeathers = MakeFeathers(Vector3.Up, 75f, 0.2f);
		_impactDust = MakeDust();
		PrecomputeRest();
		Restart();
	}

	public override void _Process(double delta)
	{
		_clock += delta;
		float t = (float)_clock;

		if (t > TEnd)
		{
			// Rendered on its own with Movie Maker: stop so the file is finalised.
			// (Inside the game Loop is off and the game takes over on Finished.)
			if (Loop && OS.HasFeature("movie"))
			{
				GetTree().Quit();
				return;
			}
			if (!Loop && !_finished)
			{
				_finished = true;
				EmitSignal(SignalName.Finished);
			}
			if (Loop && t > TEnd + LoopPause)
			{
				Restart();
				t = 0f;
			}
		}

		UpdateSeagull(t, (float)delta);
		UpdateShutter(t);
		UpdateCamera(t, (float)delta);
	}

	private void Restart()
	{
		_clock = 0;
		_windowBurstDone = _impactDone = false;
		_look = WindowCentre;
		_shake = 0f;
		_flapPhase = 0f;
	}

	// ---------------------------------------------------------------- seagull motion

	private void UpdateSeagull(float t, float dt)
	{
		Vector3 position;
		Basis basis;

		if (t < TWindow)
		{
			// Struggling flight: accelerating along the path, lopsided bank toward the
			// broken wing, dips and recoveries. All of it fades out at the window so the
			// hand-off to the fall is seamless.
			float s = Mathf.Pow(t / TWindow, 1.25f);
			position = SamplePath(s);
			Vector3 ahead = SamplePath(Mathf.Min(s + 0.003f, 1f)) - position;
			Vector3 flat = new Vector3(ahead.X, 0f, ahead.Z).Normalized();
			float k = 1f - Smooth(0.82f, 1f, s);

			Vector3 right = flat.Cross(Vector3.Up).Normalized();
			position += right * (0.45f * Mathf.Sin(t * 2.1f) * k);
			position.Y += (-0.5f * Mathf.Pow(Mathf.Sin(t * 1.7f), 2f) + 0.15f * Mathf.Sin(t * 7.5f)) * k;

			// Wing beats: faster as it gets desperate; the body heaves up on each
			// down-stroke and rolls toward the bad wing.
			_flapPhase += Mathf.Tau * (2.6f + 1.0f * s) * dt;
			position.Y += 0.06f * Mathf.Cos(_flapPhase) * k;
			float bank = (0.3f + 0.2f * Mathf.Sin(_flapPhase + 0.6f) + 0.25f * Mathf.Sin(t * 2.3f)) * k;
			float pitch = 0.12f * Mathf.Sin(_flapPhase + 1.2f) * k;
			basis = Basis.LookingAt(flat, Vector3.Up)
				* new Basis(Vector3.Back, bank)
				* new Basis(Vector3.Right, pitch);

			// Last stretch: wings swept up into a V to squeeze through the arch.
			float tuck = Smooth(TWindow - 0.4f, TWindow - 0.08f, t);
			PoseWings(FlapPose(_flapPhase), TuckPose, tuck);
			ShowFlyer(true);
		}
		else
		{
			float tau = t - TWindow;
			if (!_windowBurstDone)
			{
				_windowBurstDone = true;
				Burst(_windowFeathers, WindowPoint);
			}

			Vector3 heading = new Vector3(FallVelocity.X, 0f, FallVelocity.Z).Normalized();
			Basis facing = Basis.LookingAt(heading, Vector3.Up);
			float wobble = 0.5f * Mathf.Sin(tau * 9f) * Mathf.Exp(-tau * 1.5f);

			if (tau < FallTime)
			{
				// Out of steam: ballistic arc, tumbling nose-over, wings flailing.
				_flapPhase += Mathf.Tau * 3.4f * dt;
				PoseWings(TuckPose, FlailPose(_flapPhase), Smooth(0f, 0.15f, tau));
				ShowFlyer(true);
				position = WindowPoint + FallVelocity * tau + 0.5f * Gravity * tau * tau * Vector3.Down;
				basis = facing * new Basis(Vector3.Back, wobble) * new Basis(Vector3.Right, -9f * tau);
				position.Y = Mathf.Max(position.Y, ContactHeight(basis));
			}
			else if (tau < FallTime + BounceTime)
			{
				float b = tau - FallTime;
				ShowFlyer(false);
				if (!_impactDone)
				{
					_impactDone = true;
					Vector3 hit = LandingPoint() with { Y = 0.05f };
					Burst(_impactFeathers, hit + new Vector3(0f, 0.1f, 0f));
					Burst(_impactDust, hit);
					_shake = 1f;
				}

				Vector3 land = LandingPoint();
				position = land + new Vector3(BounceVelocity.X, 0f, BounceVelocity.Z) * b;
				basis = facing * new Basis(Vector3.Back, wobble) * new Basis(Vector3.Right, -9f * FallTime - 7f * b);
				float hop = Mathf.Max(0f, BounceVelocity.Y * b - 0.5f * Gravity * b * b);
				position.Y = ContactHeight(basis) + hop;
			}
			else if (tau < FallTime + BounceTime + RollTime)
			{
				ShowFlyer(false);
				// Rolling along the boards, losing speed, twisting toward its bad side;
				// in the last stretch it flops over onto its back.
				float r = tau - FallTime - BounceTime;
				float v0 = 1.9f;
				float dist = v0 * r - v0 * r * r / (2f * RollTime);
				Vector3 start = RollStart();
				Vector3 dir = new Vector3(BounceVelocity.X, 0f, BounceVelocity.Z).Normalized();
				position = start + dir * dist;

				float spin = 2.4f * (r - r * r / (2f * RollTime));
				float pitchAngle = -9f * FallTime - 7f * BounceTime - dist / RollRadius;
				Basis rolling = facing * new Basis(Vector3.Up, spin)
					* new Basis(Vector3.Back, wobble) * new Basis(Vector3.Right, pitchAngle);

				float settle = Smooth(0.55f, 1f, r / RollTime);
				basis = new Basis(rolling.GetRotationQuaternion().Slerp(_restBasis.GetRotationQuaternion(), settle));
				position.Y = ContactHeight(basis);
			}
			else if (t < TGetUp)
			{
				ShowFlyer(false);
				// Flat on its back, dazed. A couple of feeble twitches.
				basis = _restBasis;
				float twitch = Envelope(t, 6.9f, 0.35f) * 0.06f * Mathf.Sin(t * 55f)
					+ Envelope(t, 7.55f, 0.2f) * 0.05f * Mathf.Sin(t * 48f);
				basis *= new Basis(Vector3.Back, twitch);
				position = _restPosition with { Y = ContactHeight(basis) };
			}
			else
			{
				ShowFlyer(false);
				// Flips itself upright with a little hop, then stands there, woozy.
				float w = Smooth(0f, 1f, (t - TGetUp) / GetUpTime);
				Basis upright = UprightFacingCamera(_restPosition);
				float sway = Mathf.Exp(-(t - TGetUp - GetUpTime) * 0.8f) * Smooth(0.9f, 1f, w);
				upright *= new Basis(Vector3.Up, 0.18f * Mathf.Sin(t * 3f) * sway)
					* new Basis(Vector3.Back, 0.1f * Mathf.Sin(t * 2.3f) * sway);
				basis = new Basis(_restBasis.GetRotationQuaternion().Slerp(upright.GetRotationQuaternion(), w));
				float hop = 0.18f * Mathf.Sin(Mathf.Pi * Mathf.Clamp((t - TGetUp) / GetUpTime, 0f, 1f));
				position = _restPosition with { Y = ContactHeight(basis) + hop };
			}
		}

		_bird.Transform = new Transform3D(basis, position);
	}

	private Vector3 LandingPoint()
	{
		Vector3 p = WindowPoint + FallVelocity * FallTime + 0.5f * Gravity * FallTime * FallTime * Vector3.Down;
		return p with { Y = FloorY };
	}

	private Vector3 RollStart() =>
		LandingPoint() + new Vector3(BounceVelocity.X, 0f, BounceVelocity.Z) * BounceTime;

	private void PrecomputeRest()
	{
		Vector3 dir = new Vector3(BounceVelocity.X, 0f, BounceVelocity.Z).Normalized();
		float v0 = 1.9f;
		_restPosition = RollStart() + dir * (v0 * RollTime / 2f);
		// On its back, a touch tipped toward the camera so the face still reads.
		_restBasis = UprightFacingCamera(_restPosition) * new Basis(Vector3.Back, Mathf.DegToRad(160f));
	}

	private static Basis UprightFacingCamera(Vector3 from)
	{
		Vector3 toCam = CameraPosition - from;
		return Basis.LookingAt(new Vector3(toCam.X, 0f, toCam.Z).Normalized(), Vector3.Up);
	}

	/// <summary>Height for the pivot so the lowest point of the mesh touches the floor.</summary>
	private float ContactHeight(Basis basis)
	{
		float min = float.MaxValue;
		foreach (var p in _contactPoints)
			min = Mathf.Min(min, (basis * p).Y);
		return FloorY - min;
	}

	private static Vector3 SamplePath(float s)
	{
		// Uniform Catmull-Rom through FlightPath[1..^2]; the ends are phantom points.
		int segments = FlightPath.Length - 3;
		float f = Mathf.Clamp(s, 0f, 1f) * segments;
		int i = Math.Min((int)f, segments - 1);
		float u = f - i;
		Vector3 p0 = FlightPath[i], p1 = FlightPath[i + 1], p2 = FlightPath[i + 2], p3 = FlightPath[i + 3];
		return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u
			+ (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
	}

	// ---------------------------------------------------------------- shutter and camera

	private void UpdateShutter(float t)
	{
		// Creaks in the draught; slams back on its hinge as the bird barges past.
		float angle = 100f + 1.2f * Mathf.Sin(t * 0.8f);
		if (t > TWindow - 0.05f)
		{
			float tau = t - TWindow + 0.05f;
			angle += 16f * Mathf.Exp(-tau * 5f) * Mathf.Sin(tau * 22f);
		}
		_shutter.Rotation = new Vector3(0f, Mathf.DegToRad(angle), 0f);
	}

	private void UpdateCamera(float t, float dt)
	{
		Vector3 bird = _bird.GlobalPosition;
		Vector3 target;
		float rate;
		if (t < TWindow)
		{
			// Looking out at the fog; the eye is drawn to the bird as it closes in, but
			// the window stays in frame.
			Vector3 drift = new(0.15f * Mathf.Sin(t * 0.4f), 0.05f * Mathf.Sin(t * 0.3f), 0f);
			float w = Smooth(2.2f, 3.8f, t) * 0.55f;
			target = (WindowCentre + drift).Lerp(bird, w);
			rate = 3f;
		}
		else
		{
			target = bird + new Vector3(0f, 0.05f, 0f);
			rate = t < TWindow + FallTime + BounceTime + RollTime ? 8f : 3f;
		}
		_look = _look.Lerp(target, 1f - Mathf.Exp(-rate * dt));

		// A flinch as the bird comes through, then a jolt on impact.
		float flinch = Envelope(t, TWindow + 0.1f, 0.5f) * 0.05f;
		_shake = Mathf.Max(0f, _shake - dt * 4f);
		Vector3 shake = new Vector3(Mathf.Sin(t * 71f), Mathf.Sin(t * 53f), 0f) * 0.012f * _shake * _shake;
		Vector3 breathe = new(0f, 0.008f * Mathf.Sin(t * 1.6f), 0f);

		// Eyes on the window while it's quiet; the view widens as the bird bursts in.
		_camera.Fov = Mathf.Lerp(52f, 64f, Smooth(TWindow - 0.3f, TWindow + 0.5f, t));

		Vector3 eye = CameraPosition + breathe + shake + new Vector3(0f, -flinch, flinch * 0.5f);
		_camera.GlobalTransform = new Transform3D(Basis.LookingAt(_look - eye, Vector3.Up), eye);
	}

	// ---------------------------------------------------------------- setup

	private void PrepareSeagull()
	{
		_bird = GetNode<Node3D>("Seagull");
		_model = _bird.GetNode<Node3D>("Model");

		// Bounds and a sample of vertices in the model's own space.
		var meshes = new List<(MeshInstance3D Mesh, Transform3D ToModel)>();
		foreach (var node in _model.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mi = (MeshInstance3D)node;
			meshes.Add((mi, _model.GlobalTransform.AffineInverse() * mi.GlobalTransform));
		}

		Aabb bounds = default;
		bool first = true;
		foreach (var (mi, toModel) in meshes)
		{
			Aabb box = toModel * mi.GetAabb();
			bounds = first ? box : bounds.Merge(box);
			first = false;
		}

		float scale = SeagullHeight / bounds.Size.Y;
		Basis b = new Basis(Vector3.Up, Mathf.DegToRad(ModelYawDegrees)).Scaled(Vector3.One * scale);
		var modelTransform = new Transform3D(b, -(b * bounds.GetCenter()));
		_model.Transform = modelTransform;

		// Contact points in pivot space, so the bird can be kept on top of the floor
		// whatever way up it is.
		foreach (var (mi, toModel) in meshes)
		{
			for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
			{
				var verts = mi.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int stride = Math.Max(1, verts.Length / 800);
				for (int i = 0; i < verts.Length; i += stride)
					_contactPoints.Add(modelTransform * (toModel * verts[i]));
			}
		}
	}

	// ---------------------------------------------------------------- wings

	/// <summary>Up-angles in radians: left inner, left outer, right inner, right outer.
	/// The right wing is the broken one.</summary>
	private readonly record struct WingPose(float LeftInner, float LeftOuter, float RightInner, float RightOuter)
	{
		public WingPose Lerp(WingPose b, float w) => new(
			Mathf.Lerp(LeftInner, b.LeftInner, w), Mathf.Lerp(LeftOuter, b.LeftOuter, w),
			Mathf.Lerp(RightInner, b.RightInner, w), Mathf.Lerp(RightOuter, b.RightOuter, w));
	}

	private static readonly WingPose TuckPose = new(1.35f, 0.35f, 1.15f, 0.2f);

	/// <summary>Good wing: big, even beats with the tip lagging. Broken wing: hangs low,
	/// twitches out of step and never gets above the body.</summary>
	private static WingPose FlapPose(float phase) => new(
		0.15f + 0.8f * Mathf.Sin(phase),
		0.05f + 0.5f * Mathf.Sin(phase - 0.8f),
		-0.45f + 0.28f * Mathf.Sin(phase + 0.5f) + 0.12f * Mathf.Sin(phase * 2.7f),
		-0.7f + 0.3f * Mathf.Sin(phase * 1.9f + 1f));

	private static WingPose FlailPose(float phase) => new(
		0.3f + 0.9f * Mathf.Sin(phase * 1.3f),
		0.6f * Mathf.Sin(phase * 1.3f - 1f),
		-0.2f + 0.7f * Mathf.Sin(phase * 1.1f + 2f),
		-0.5f + 0.5f * Mathf.Sin(phase * 2f));

	private void PoseWings(WingPose a, WingPose b, float w)
	{
		if (_wings == null)
			return;
		var p = a.Lerp(b, w);
		SetWing("wing_l_inner", p.LeftInner, +1f);
		SetWing("wing_l_outer", p.LeftOuter, +1f);
		SetWing("wing_r_inner", p.RightInner, -1f);
		SetWing("wing_r_outer", p.RightOuter, -1f);
	}

	/// <summary>Raises a wing bone by <paramref name="up"/> radians about the body's
	/// forward axis. The wings mirror each other, so the right side turns the other way.</summary>
	private void SetWing(string name, float up, float side)
	{
		if (!_wingBones.TryGetValue(name, out var bone))
			return;
		_wings.SetBonePoseRotation(bone.Bone, bone.RestRotation * new Quaternion(bone.Axis, up * side));
	}

	private void ShowFlyer(bool flying)
	{
		if (_flyer == null)
			return;
		_flyer.Visible = flying;
		_model.Visible = !flying;
	}

	private void PrepareFlyer()
	{
		_flyer = _bird.GetNodeOrNull<Node3D>("FlyingModel");
		if (_flyer == null)
			return;

		Aabb bounds = default;
		bool first = true;
		foreach (var node in _flyer.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mi = (MeshInstance3D)node;
			Aabb box = _flyer.GlobalTransform.AffineInverse() * mi.GlobalTransform * mi.GetAabb();
			bounds = first ? box : bounds.Merge(box);
			first = false;
		}
		float scale = FlyingWingspan / bounds.Size.X;
		Basis b = new Basis(Vector3.Up, Mathf.DegToRad(ModelYawDegrees)).Scaled(Vector3.One * scale);
		_flyer.Transform = new Transform3D(b, -(b * bounds.GetCenter()));

		_wings = _flyer.FindChildren("*", "Skeleton3D", true, false) is { Count: > 0 } found ? (Skeleton3D)found[0] : null;
		if (_wings == null)
			return;

		// The model's forward (+Z in its own space) expressed in the skeleton's space; each
		// wing bone flaps about that axis, taken into the bone's own frame.
		Vector3 forward = (_wings.GlobalBasis.Inverse() * _flyer.GlobalBasis * Vector3.Back).Normalized();
		foreach (var name in new[] { "wing_l_inner", "wing_l_outer", "wing_r_inner", "wing_r_outer" })
		{
			int bone = _wings.FindBone(name);
			if (bone < 0)
				continue;
			Basis globalRest = _wings.GetBoneGlobalRest(bone).Basis.Orthonormalized();
			Vector3 axis = (globalRest.Inverse() * forward).Normalized();
			_wingBones[name] = (bone, _wings.GetBoneRest(bone).Basis.GetRotationQuaternion(), axis);
		}
	}

	private CpuParticles3D MakeFeathers(Vector3 direction, float spread, float radius)
	{
		var mat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.97f, 0.97f, 0.95f),
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			Roughness = 1f,
		};
		var fade = new Curve();
		fade.AddPoint(new Vector2(0f, 1f));
		fade.AddPoint(new Vector2(0.75f, 1f));
		fade.AddPoint(new Vector2(1f, 0f));

		var p = new CpuParticles3D
		{
			Emitting = false,
			OneShot = true,
			Amount = 28,
			Lifetime = 3.2,
			Explosiveness = 0.95f,
			LocalCoords = false,
			Mesh = new QuadMesh { Size = new Vector2(0.07f, 0.028f), Material = mat },
			EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = radius,
			Direction = direction,
			Spread = spread,
			InitialVelocityMin = 1.0f,
			InitialVelocityMax = 2.8f,
			Gravity = new Vector3(0f, -0.45f, 0f),
			DampingMin = 2.0f,
			DampingMax = 3.5f,
			AngleMin = -180f,
			AngleMax = 180f,
			AngularVelocityMin = -540f,
			AngularVelocityMax = 540f,
			ScaleAmountMin = 0.6f,
			ScaleAmountMax = 1.4f,
			ScaleAmountCurve = fade,
		};
		p.SetParticleFlag(CpuParticles3D.ParticleFlags.RotateY, true);
		AddChild(p);
		return p;
	}

	private CpuParticles3D MakeDust()
	{
		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			BillboardKeepScale = true,
			VertexColorUseAsAlbedo = true,
			AlbedoColor = new Color(0.72f, 0.68f, 0.6f, 0.4f),
			AlbedoTexture = new GradientTexture2D
			{
				Gradient = new Gradient
				{
					Offsets = new[] { 0f, 1f },
					Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f) },
				},
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.5f),
				FillTo = new Vector2(1f, 0.5f),
				Width = 64,
				Height = 64,
			},
		};
		var ramp = new Gradient();
		ramp.SetColor(0, new Color(1f, 1f, 1f, 0.9f));
		ramp.SetColor(1, new Color(1f, 1f, 1f, 0f));
		var grow = new Curve();
		grow.AddPoint(new Vector2(0f, 0.35f));
		grow.AddPoint(new Vector2(1f, 1f));

		var p = new CpuParticles3D
		{
			Emitting = false,
			OneShot = true,
			Amount = 18,
			Lifetime = 1.4,
			Explosiveness = 1f,
			LocalCoords = false,
			Mesh = new QuadMesh { Size = new Vector2(0.35f, 0.35f), Material = mat },
			EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.15f,
			Direction = Vector3.Up,
			Spread = 85f,
			Flatness = 0.6f,
			InitialVelocityMin = 0.5f,
			InitialVelocityMax = 1.4f,
			Gravity = new Vector3(0f, 0.05f, 0f),
			DampingMin = 2.5f,
			DampingMax = 4f,
			AngleMin = -180f,
			AngleMax = 180f,
			ScaleAmountCurve = grow,
			ColorRamp = ramp,
		};
		AddChild(p);
		return p;
	}

	private static void Burst(CpuParticles3D p, Vector3 at)
	{
		p.GlobalPosition = at;
		p.Restart();
		p.Emitting = true;
	}

	private static float Smooth(float edge0, float edge1, float x) =>
		Mathf.SmoothStep(edge0, edge1, x);

	/// <summary>0 outside [start, start+length], a smooth bump inside.</summary>
	private static float Envelope(float t, float start, float length)
	{
		if (t < start || t > start + length) return 0f;
		return Mathf.Sin(Mathf.Pi * (t - start) / length);
	}
}
