using Godot;
using System.Collections.Generic;

/// <summary>
/// Night at the Strange Oak. The foxes' ring dance in the stone circle: foxes standing on two
/// legs, hand in hand, creeping sideways round the tree, their tails swaying. And fireflies
/// drifting about in front of the oak, flashing. Real 3D figures
/// (assets/models/fox/fox.glb, made by tools/blender_rig_fox.py) drawn over the painted
/// background through a camera matched to the painting.
/// </summary>
/// <remarks>
/// Layering, bottom to top: background, the foxes behind the tree, the trunk (cut from the
/// background), the foxes in front of the tree, the two front stones. The animation moves the
/// legs; the arms are held out to the neighbours and the tails swayed here, in code.
/// Only there while <see cref="ShowIf"/> holds (by default after the Tavern Keeper's death).
/// </remarks>
[GlobalClass]
public partial class FoxDance : Control
{
	[Export] public string ShowIf { get; set; } = "tavern_keeper_dead";
	[Export] public int Count { get; set; } = 15;
	/// <summary>Size of the foxes (1 = the model's 1.9 m). Smaller foxes reach less far, so the
	/// ring needs more of them to stay hand in hand: about 15 at 0.65.</summary>
	[Export] public float FoxScale { get; set; } = 0.65f;
	/// <summary>Radius of the dancing ring, metres (the stone circle is 4).</summary>
	[Export] public float Radius { get; set; } = 2.5f;
	/// <summary>How fast the ring turns, metres per second along it.</summary>
	[Export] public float StepSpeed { get; set; } = 0.15f;
	/// <summary>"SideStep" (upright) or "CrouchStep" (creeping).</summary>
	[Export] public string Step { get; set; } = "CrouchStep";

	[Export] public int FireflyCount { get; set; } = 70;
	[Export] public float AnimationSpeed { get; set; } = 0.55f;

	// The camera that matches the painting of the oak (fitted to the stone circle): 35° field
	// of view, 2.95 m up, turned 10.6° left, the ring's centre 19 m ahead.
	private const float Fov = 35f;
	private static readonly Vector3 CameraPosition = new(0, 2.95f, 0);
	private static readonly Vector3 CameraRotationDegrees = new(-0.35f, -10.57f, 0);
	private static readonly Vector3 Centre = new(0, 0, -19.01f);

	private const uint BehindLayer = 1, InFrontLayer = 2;

	private sealed class Fox
	{
		public Node3D Root;
		public Skeleton3D Skeleton;
		public AnimationPlayer Player;
		public List<GeometryInstance3D> Meshes = new();
		public float Angle;
		public int[] Bones; // LeftArm, LeftForeArm, LeftHand, RightArm, RightForeArm, RightHand, Hips, Tail1..4
		public float Seed;
	}

	private readonly List<Fox> _foxes = new();

	private sealed class Firefly
	{
		public Vector3 Home;           // middle of where it drifts
		public Vector3 Drift;          // how far it drifts each way
		public Vector3 Rate;           // how fast
		public float Phase, FlashRate;
	}
	private readonly List<Firefly> _flies = new();
	private MultiMesh _flyMesh;

	private SubViewport _behind, _inFront;
	private float _time;
	private float _direction = 1f;   // which way round makes "stepping to the left" for a fox facing the tree

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		if (Engine.IsEditorHint())
			return;

		Visible = GameState.Instance.Check(ShowIf);
		GameState.Instance.Changed += OnChanged;
		if (!Visible)
			return;
		Build();
	}

	public override void _ExitTree()
	{
		if (GameState.Instance != null)
			GameState.Instance.Changed -= OnChanged;
	}

	private void OnChanged()
	{
		bool show = GameState.Instance.Check(ShowIf);
		if (show && _foxes.Count == 0)
			Build();
		Visible = show;
	}

	private void Build()
	{
		var size = (Vector2I)GetViewportRect().Size;
		var shared = new World3D();   // both views look into the same world
		_behind = View(size, BehindLayer, shared);
		AddLayer(_behind);
		AddChild(Picture("res://assets/art/props/strange_oak_trunk.png"));
		_inFront = View(size, InFrontLayer, shared);
		AddLayer(_inFront);
		AddChild(Picture("res://assets/art/props/strange_oak_stones.png"));

		var world = new Node3D { Name = "World" };
		_behind.AddChild(world);
		world.AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.ClearColor,
				AmbientLightSource = Godot.Environment.AmbientSource.Color,
				AmbientLightColor = new Color(0.32f, 0.38f, 0.55f),
				AmbientLightEnergy = 0.7f,
				FogEnabled = true,
				// A little of the night mist the painting has, in its colour.
				FogLightColor = new Color(0.16f, 0.22f, 0.34f),
				FogDensity = 0.007f,
				FogSkyAffect = 0f,
			},
		});
		// Moonlight from high behind the tree, a weak cold fill from the front.
		world.AddChild(new DirectionalLight3D
		{
			LightColor = new Color(0.62f, 0.72f, 1f),
			LightEnergy = 1.5f,
			Rotation = new Vector3(Mathf.DegToRad(-50), Mathf.DegToRad(160), 0),
		});
		world.AddChild(new DirectionalLight3D
		{
			LightColor = new Color(0.5f, 0.58f, 0.8f),
			LightEnergy = 0.65f,
			Rotation = new Vector3(Mathf.DegToRad(-15), Mathf.DegToRad(-10), 0),
		});

		var scene = GD.Load<PackedScene>("res://assets/models/fox/fox.glb");
		var shadowMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/blob_shadow.gdshader") };
		for (int i = 0; i < Count; i++)
		{
			var root = scene.Instantiate<Node3D>();
			world.AddChild(root);
			var fox = new Fox { Root = root, Angle = Mathf.Tau * i / Count, Seed = i * 1.37f };
			fox.Skeleton = root.FindChildren("*", "Skeleton3D", true, false)[0] as Skeleton3D;
			fox.Player = root.FindChildren("*", "AnimationPlayer", true, false)[0] as AnimationPlayer;
			foreach (var node in root.FindChildren("*", "GeometryInstance3D", true, false))
				fox.Meshes.Add((GeometryInstance3D)node);
			var shadow = new MeshInstance3D
			{
				Mesh = new PlaneMesh { Size = new Vector2(1.1f, 0.8f) },
				MaterialOverride = shadowMaterial,
				Position = new Vector3(0, 0.02f, 0),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			root.AddChild(shadow);
			fox.Meshes.Add(shadow);

			var s = fox.Skeleton;
			fox.Bones = new[]
			{
				s.FindBone("LeftArm"), s.FindBone("LeftForeArm"), s.FindBone("LeftHand"),
				s.FindBone("RightArm"), s.FindBone("RightForeArm"), s.FindBone("RightHand"),
				s.FindBone("Hips"), s.FindBone("Tail1"), s.FindBone("Tail2"), s.FindBone("Tail3"), s.FindBone("Tail4"),
			};

			var anim = fox.Player.GetAnimation(Step) ?? fox.Player.GetAnimation(fox.Player.GetAnimationList()[0]);
			anim.LoopMode = Godot.Animation.LoopModeEnum.Linear;
			fox.Player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
			fox.Player.Play(fox.Player.HasAnimation(Step) ? Step : fox.Player.GetAnimationList()[0]);
			// All in step, as in a ring dance, give or take a hair.
			fox.Player.Seek(i * 0.03, true);
			_foxes.Add(fox);
		}
		Place(0f);
		BuildFireflies(world);

		// Step towards the fox's own left: work out which way round that is.
		var first = _foxes[0];
		Vector3 left = first.Root.GlobalBasis.X;
		Vector3 tangent = new(-Mathf.Sin(first.Angle), 0, Mathf.Cos(first.Angle));
		_direction = left.Dot(tangent) >= 0 ? 1f : -1f;
	}

	private SubViewport View(Vector2I size, uint layer, World3D shared)
	{
		var view = new SubViewport
		{
			Size = size,
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			Msaa3D = Viewport.Msaa.Msaa4X,
			World3D = shared,
		};
		view.AddChild(new Camera3D
		{
			Fov = Fov,
			Position = CameraPosition,
			RotationDegrees = CameraRotationDegrees,
			CullMask = layer,
			Current = true,
		});
		AddChild(view);
		return view;
	}

	private void AddLayer(SubViewport view)
	{
		var picture = new TextureRect
		{
			Texture = view.GetTexture(),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(picture);
	}

	/// <summary>A layer cut from the background, covering the screen the same way it does.</summary>
	private static TextureRect Picture(string path)
	{
		var texture = GD.Load<Texture2D>(path);
		var layer = new TextureRect
		{
			Texture = Night.Variant(texture, out _),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		return layer;
	}

	public override void _Process(double delta)
	{
		if (!Visible || _foxes.Count == 0)
			return;
		float dt = (float)delta;
		_time += dt;
		Place(_direction * StepSpeed / Radius * dt);
		foreach (var fox in _foxes)
		{
			fox.Player.Advance(dt * AnimationSpeed);
			HoldHands(fox);
			SwayTail(fox);
		}
		MoveFireflies();
	}

	// ---------------------------------------------------------------- fireflies

	private void BuildFireflies(Node3D world)
	{
		var rng = new RandomNumberGenerator { Seed = 7 };

		for (int i = 0; i < FireflyCount; i++)
		{
			var fly = new Firefly
			{
				Home = Centre + new Vector3(rng.RandfRange(-4.5f, 4.5f), rng.RandfRange(0.5f, 4.2f), rng.RandfRange(0.8f, 4.5f)),
				Drift = new Vector3(rng.RandfRange(0.4f, 1.0f), rng.RandfRange(0.2f, 0.6f), rng.RandfRange(0.3f, 0.8f)),
				Rate = new Vector3(rng.RandfRange(0.1f, 0.3f), rng.RandfRange(0.15f, 0.4f), rng.RandfRange(0.1f, 0.25f)),
				Phase = rng.RandfRange(0f, Mathf.Tau),
				FlashRate = rng.RandfRange(0.6f, 1.6f),
			};
			_flies.Add(fly);
		}

		_flyMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseCustomData = true,
			Mesh = new QuadMesh { Size = new Vector2(0.2f, 0.2f) },
			InstanceCount = _flies.Count,
		};
		world.AddChild(new MultiMeshInstance3D
		{
			Multimesh = _flyMesh,
			MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/firefly.gdshader") },
			Layers = InFrontLayer,   // in front of the trunk; foxes passing in front still hide them
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		});
		MoveFireflies();
	}

	private void MoveFireflies()
	{
		if (_flyMesh == null)
			return;
		for (int i = 0; i < _flies.Count; i++)
		{
			var f = _flies[i];
			float t = _time + f.Phase * 3f;
			var drifting = f.Home + new Vector3(
				Mathf.Sin(t * f.Rate.X + f.Phase) * f.Drift.X,
				Mathf.Sin(t * f.Rate.Y + f.Phase * 1.7f) * f.Drift.Y,
				Mathf.Sin(t * f.Rate.Z + f.Phase * 2.3f) * f.Drift.Z);
			// Real fireflies flash rather than glow.
			float flash = 0.15f + 0.85f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * f.FlashRate + f.Phase)), 6f);

			_flyMesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, drifting));
			_flyMesh.SetInstanceCustomData(i, new Color(flash, 0, 0, 0));
		}
	}


	/// <summary>Moves the ring round by <paramref name="turn"/> radians; each fox faces the tree.</summary>
	private void Place(float turn)
	{
		foreach (var fox in _foxes)
		{
			fox.Angle += turn;
			var position = Centre + new Vector3(Mathf.Cos(fox.Angle), 0, Mathf.Sin(fox.Angle)) * Radius;
			fox.Root.Position = position;
			// The model faces +Z; turn it so +Z points at the tree.
			Vector3 toTree = (Centre - position).Normalized();
			fox.Root.Basis = new Basis(Vector3.Up, Mathf.Atan2(toTree.X, toTree.Z)).Scaled(Vector3.One * FoxScale);
			uint layer = position.Z < Centre.Z ? BehindLayer : InFrontLayer;
			foreach (var mesh in fox.Meshes)
				mesh.Layers = layer;
		}
	}

	/// <summary>Arms out to the sides, a little down and a little in, to the neighbours' paws.</summary>
	private void HoldHands(Fox fox)
	{
		float inward = Mathf.Pi / Count;   // the neighbours are round the curve
		float down = Mathf.DegToRad(24f + 4f * Mathf.Sin(_time * 1.1f + fox.Seed));
		for (int side = 0; side < 2; side++)
		{
			float x = side == 0 ? 1f : -1f;   // the model's left is +X
			var dir = new Vector3(x * Mathf.Cos(down) * Mathf.Cos(inward), -Mathf.Sin(down), Mathf.Cos(down) * Mathf.Sin(inward));
			Vector3 world = fox.Root.GlobalBasis * dir;
			int arm = fox.Bones[side * 3], fore = fox.Bones[side * 3 + 1], hand = fox.Bones[side * 3 + 2];
			Aim(fox.Skeleton, arm, fore, world);
			Aim(fox.Skeleton, fore, hand, world);
		}
	}

	/// <summary>
	/// The tail: a slow sway out to the side and back, each joint a little behind the one
	/// before, now and then lifting into a curl. Never swung in towards the legs.
	/// </summary>
	private void SwayTail(Fox fox)
	{
		float t = _time * 0.9f + fox.Seed;
		float curl = 0.5f + 0.5f * Mathf.Sin(t * 0.31f);       // the slow, unsettling part
		Vector3 up = fox.Root.GlobalBasis.Y;
		Vector3 side = fox.Root.GlobalBasis.X;
		for (int k = 0; k < 4; k++)
		{
			int bone = fox.Bones[7 + k];
			if (bone < 0)
				continue;
			// Nothing animates the tail, so start each frame from rest rather than piling up.
			fox.Skeleton.ResetBonePose(bone);
			float lag = t - k * 0.55f;
			float sway = (0.12f + 0.1f * k) * (0.6f + 0.4f * Mathf.Sin(lag));
			float lift = (0.08f + 0.12f * k) * curl * (0.7f + 0.3f * Mathf.Sin(lag * 1.3f));
			Rotate(fox.Skeleton, bone, (new Quaternion(up.Normalized(), sway) * new Quaternion(side.Normalized(), -lift)).Normalized());
		}
	}

	/// <summary>Turns <paramref name="bone"/> so it points from itself to <paramref name="child"/> along a world direction.</summary>
	private static void Aim(Skeleton3D skeleton, int bone, int child, Vector3 worldDirection)
	{
		if (bone < 0 || child < 0)
			return;
		var toSkeleton = skeleton.GlobalBasis.Inverse();
		Vector3 target = (toSkeleton * worldDirection).Normalized();
		var global = skeleton.GetBoneGlobalPose(bone);
		Vector3 current = (skeleton.GetBoneGlobalPose(child).Origin - global.Origin).Normalized();
		if (current.LengthSquared() < 1e-6f || current.IsEqualApprox(target))
			return;
		SetGlobalRotation(skeleton, bone, new Basis(new Quaternion(current, target)) * global.Basis);
	}

	/// <summary>Applies a world-space rotation on top of the bone's current pose.</summary>
	private static void Rotate(Skeleton3D skeleton, int bone, Quaternion worldRotation)
	{
		var toSkeleton = skeleton.GlobalBasis.Orthonormalized();
		Basis inSkeleton = toSkeleton.Inverse() * new Basis(worldRotation) * toSkeleton;
		SetGlobalRotation(skeleton, bone, inSkeleton * skeleton.GetBoneGlobalPose(bone).Basis);
	}

	private static void SetGlobalRotation(Skeleton3D skeleton, int bone, Basis global)
	{
		int parent = skeleton.GetBoneParent(bone);
		Basis parentGlobal = parent >= 0 ? skeleton.GetBoneGlobalPose(parent).Basis : Basis.Identity;
		Basis local = parentGlobal.Inverse() * global;
		skeleton.SetBonePoseRotation(bone, local.Orthonormalized().GetRotationQuaternion());
	}
}
