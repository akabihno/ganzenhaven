using Godot;
using System.Collections.Generic;

/// <summary>
/// Turns the blocky Blender office into the dark medieval harbourmaster's office of
/// assets/concepts/office_interior_b2.png: stone-and-plaster walls, soot-blackened timber,
/// worn oak boards, a twin-arched window with open oak shutters, a smouldering
/// hearth, and Meshy props in place of the old primitive furniture.
/// The arches are glazed with small leaded panes; the seagull smashes through the right one.
/// </summary>
/// <remarks>
/// <para>Applied at load time rather than baked into the office glTF, so the Blender
/// source can still be re-exported. [Tool], so the editor shows the same room. Everything it
/// adds has no owner and every change is on nodes inside the instanced glTF, so none of it
/// is written into the .tscn.</para>
/// <para>Coordinates are the office glTF's: floor at y = 0, the window wall's inner face at
/// z = -5, side walls at x = ±3.</para>
/// </remarks>
[Tool]
[GlobalClass]
public partial class OfficeRestyle : Node3D
{
	[Export] public NodePath OfficePath { get; set; } = "../Office";

	/// <summary>Shutter on the right-hand arch, the one the seagull comes through.</summary>
	public Node3D RightShutter { get; private set; }

	/// <summary>Leaded glass in the right-hand arch; <see cref="ShatterGlass"/> breaks it.</summary>
	public Node3D RightArchGlass { get; private set; }

	/// <summary>Where the fire burns, for its sound.</summary>
	public Vector3 FirePosition { get; private set; }

	/// <summary>Where a bird flying through the right-hand arch crosses the wall.</summary>
	public static readonly Vector3 RightArchCentre = new(ArchCentreX, 1.7f, -5.1f);

	private const float WallFace = -4.995f;
	private const float ArchCentreX = 0.51f;
	private const float ArchRadius = 0.36f;
	private const float ArchSpring = 2.0f;   // where the round top starts
	private const float SillY = 0.98f;

	private OmniLight3D _fireLight, _candleLight;
	private Node3D _shards;
	private StandardMaterial3D _shardMaterial;
	private readonly List<StandardMaterial3D> _embers = new();
	private double _clock;

	private static readonly (string Path, Vector3 Position, float YawDegrees, string Fit, float Size, Color Tint)[] Props =
	{
		// Fit: "w" = scale to this width (x), "h" = to this height.
		("res://assets/models/props/hearth/model.glb", new(-2.05f, 0f, 0f), 0f, "w", 1.8f, new(0.8f, 0.78f, 0.75f)),
		("res://assets/models/props/trestle_table/model.glb", new(-1.95f, 0f, -1.55f), 75f, "w", 1.9f, new(0.85f, 0.8f, 0.75f)),
		("res://assets/models/props/stool/model.glb", new(-1.0f, 0f, -2.95f), 25f, "h", 0.48f, new(0.38f, 0.3f, 0.26f)),
		("res://assets/models/props/barrels/model.glb", new(2.05f, 0f, -4.35f), -35f, "h", 0.85f, new(0.7f, 0.66f, 0.62f)),
		("res://assets/models/props/grain_sacks/model.glb", new(2.45f, 0f, -3.15f), -75f, "h", 0.72f, new(0.62f, 0.57f, 0.5f)),
	};

	public override void _Ready()
	{
		var office = GetNodeOrNull<Node3D>(OfficePath);
		if (office == null)
			return;

		HideOldDressing(office);
		Retexture(office);
		BuildWindow(office);
		foreach (var prop in Props)
			PlaceProp(prop.Path, prop.Position, prop.YawDegrees, prop.Fit, prop.Size, prop.Tint);
		BuildFire();
	}

	public override void _Process(double delta)
	{
		_clock += delta;
		float t = (float)_clock;
		// Two incommensurate wobbles read as an uneven, living flame.
		float flicker = 1f + 0.18f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 2.9f + 1.1f) + 0.07f * Mathf.Sin(t * 23f);
		if (_fireLight != null) _fireLight.LightEnergy = 1.5f * flicker;
		if (_candleLight != null) _candleLight.LightEnergy = 0.55f * (1f + 0.06f * Mathf.Sin(t * 17f));
		for (int i = 0; i < _embers.Count; i++)
			_embers[i].EmissionEnergyMultiplier = 2.2f + 1.4f * (0.5f + 0.5f * Mathf.Sin(t * (1.3f + i * 0.37f) + i));
	}

	// ---------------------------------------------------------------- old dressing

	private static readonly string[] HiddenPrefixes =
	{
		// Blender camera/lights: the scene lights itself.
		"Camera", "SunLight", "FillLight",
		// Toy-scale backdrop, replaced by FoggyHarbourShips.
		"Cog", "House_", "Roof_", "Windmill",
		// Glazed window and window seat, replaced by the arches.
		"Mullion_", "Glass_", "WindowSeat",
		// Primitive furniture, replaced by the Meshy props.
		"HarbourmasterDesk", "HarbourmasterChair", "Ledger_", "Inkwell", "Quill_", "Candlestick_",
		"Candle", "Flame_Desk", "CandleGlow_Desk", "ModelShip_", "RolledChart_",
		"VisitorTable", "VisitorChair", "VisitorStool",
		"Lantern_", "CandleGlow_Lantern",
		// Flat painted boards read wrong on rough stone.
		"Shield_", "WallMap", "MapRoll_",
		// Panelling doesn't belong on rough stone.
		"Wainscot_",
	};

	private static void HideOldDressing(Node3D office)
	{
		foreach (var node in office.FindChildren("*", "Node3D", true, false))
		{
			string name = node.Name;
			foreach (var prefix in HiddenPrefixes)
			{
				if (name.StartsWith(prefix))
				{
					((Node3D)node).Visible = false;
					break;
				}
			}
		}
	}

	// ---------------------------------------------------------------- materials

	private StandardMaterial3D _stone, _boards, _ceiling, _timber;

	private void Retexture(Node3D office)
	{
		_stone = Textured("stone_plaster_wall", 0.3f, new Color(0.72f, 0.69f, 0.64f));
		_boards = Textured("oak_floorboards", 0.55f, new Color(0.8f, 0.76f, 0.72f));
		_ceiling = Textured("oak_floorboards", 0.5f, new Color(0.32f, 0.29f, 0.27f));
		_timber = Textured("blackened_oak", 0.7f, new Color(0.75f, 0.72f, 0.7f));

		var byName = new Dictionary<string, Material>
		{
			["Mat_Plaster"] = _stone,
			["Mat_Stone"] = _stone,
			["Mat_WoodFloor"] = _boards,
			["Mat_Ceiling"] = _ceiling,
			["Mat_WoodDark"] = _timber,
			["Mat_WoodMid"] = _timber,
		};

		foreach (var node in office.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mi = (MeshInstance3D)node;
			for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
			{
				var original = mi.Mesh.SurfaceGetMaterial(s);
				if (original != null && byName.TryGetValue(original.ResourceName, out var replacement))
					mi.SetSurfaceOverrideMaterial(s, replacement);
			}
		}
	}

	private static StandardMaterial3D Textured(string name, float uvScale, Color tint) => new()
	{
		AlbedoTexture = GD.Load<Texture2D>($"res://assets/textures/{name}_albedo.png"),
		AlbedoColor = tint,
		NormalEnabled = true,
		NormalTexture = GD.Load<Texture2D>($"res://assets/textures/{name}_normal.png"),
		NormalScale = 1.0f,
		Roughness = 0.92f,
		Uv1Triplanar = true,
		Uv1WorldTriplanar = true,
		Uv1Scale = Vector3.One * uvScale,
		Uv1TriplanarSharpness = 4f,
	};

	// ---------------------------------------------------------------- window

	private void BuildWindow(Node3D office)
	{
		// Fill the old 3 m glazed opening with masonry and cut two round-headed arches
		// either side of a stone mullion.
		var wall = new CsgBox3D
		{
			Name = "WindowMasonry",
			Size = new Vector3(3.0f, 1.52f, 0.5f),
			Position = new Vector3(0f, 0.99f + 0.76f, WallFace - 0.25f),
			Material = _stone,
			UseCollision = false,
		};
		AddChild(wall);

		foreach (float x in new[] { -ArchCentreX, ArchCentreX })
		{
			float h = ArchSpring - SillY;
			wall.AddChild(new CsgBox3D
			{
				Operation = CsgShape3D.OperationEnum.Subtraction,
				Size = new Vector3(ArchRadius * 2f, h, 1.0f),
				Material = _stone,
				Position = new Vector3(x, SillY + h / 2f - wall.Position.Y, 0f),
			});
			wall.AddChild(new CsgCylinder3D
			{
				Operation = CsgShape3D.OperationEnum.Subtraction,
				Radius = ArchRadius,
				Height = 1.0f,
				Sides = 32,
				Material = _stone,
				Rotation = new Vector3(Mathf.Pi / 2f, 0f, 0f),
				Position = new Vector3(x, ArchSpring - wall.Position.Y, 0f),
			});
		}

		// Two heavy plank shutters, hinged at the outer edges of the reveal and folded
		// back into the room.
		BuildShutter(-0.9f, +1f, -100f);
		RightShutter = BuildShutter(0.9f, -1f, 100f);
		BuildGlass();
	}

	// ---------------------------------------------------------------- glass

	private const float GlassZ = WallFace - 0.2f;
	private static float GlassHeight => ArchSpring - SillY + ArchRadius;

	/// <summary>Small leaded panes filling both arches, set back in the reveal.</summary>
	private void BuildGlass()
	{
		var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/leaded_glass.gdshader") };
		material.SetShaderParameter("width", ArchRadius * 2f);
		material.SetShaderParameter("height", GlassHeight);
		material.SetShaderParameter("spring", ArchSpring - SillY);
		material.SetShaderParameter("radius", ArchRadius);
		MeshInstance3D Pane(string name, float x)
		{
			var pane = new MeshInstance3D
			{
				Name = name,
				Mesh = new QuadMesh { Size = new Vector2(ArchRadius * 2f, GlassHeight) },
				MaterialOverride = material,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				Position = new Vector3(x, SillY + GlassHeight / 2f, GlassZ),
			};
			AddChild(pane);
			return pane;
		}
		Pane("LeftArchGlass", -ArchCentreX);
		RightArchGlass = Pane("RightArchGlass", ArchCentreX);
	}

	/// <summary>
	/// Breaks the glass in the right-hand arch: the pane goes and its pieces fly as real
	/// physics bodies, most of them into the room (the way the bird was going), landing and
	/// settling on the floorboards. Deterministic for a given seed.
	/// </summary>
	public void ShatterGlass(Vector3 push, int seed = 11)
	{
		ResetGlass();
		RightArchGlass.Visible = false;

		_shards = new Node3D { Name = "GlassShards" };
		AddChild(_shards);

		// Something for the pieces to land on.
		var floor = new StaticBody3D { Name = "Floor" };
		floor.AddChild(new CollisionShape3D { Shape = new WorldBoundaryShape3D { Plane = new Plane(Vector3.Up, 0f) } });
		_shards.AddChild(floor);

		_shardMaterial ??= new StandardMaterial3D
		{
			AlbedoColor = new Color(0.42f, 0.52f, 0.47f, 0.3f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.05f,
			MetallicSpecular = 0.6f,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
		var bounce = new PhysicsMaterial { Bounce = 0.2f, Friction = 0.7f };
		var rng = new RandomNumberGenerator { Seed = (ulong)seed };

		for (int i = 0; i < 60; i++)
		{
			// A random spot inside the arch shape.
			Vector2 p;
			do
				p = new Vector2(rng.RandfRange(-ArchRadius, ArchRadius), rng.RandfRange(0f, GlassHeight));
			while (p.Y > ArchSpring - SillY && new Vector2(p.X, p.Y - (ArchSpring - SillY)).Length() > ArchRadius);

			var size = new Vector3(rng.RandfRange(0.02f, 0.07f), rng.RandfRange(0.02f, 0.06f), 0.004f);
			var shard = new RigidBody3D
			{
				Mass = 0.02f,
				PhysicsMaterialOverride = bounce,
				ContinuousCd = true,
				Position = new Vector3(ArchCentreX + p.X, SillY + p.Y, GlassZ),
				Rotation = new Vector3(rng.RandfRange(-0.4f, 0.4f), rng.RandfRange(-0.4f, 0.4f), rng.RandfRange(0f, Mathf.Tau)),
			};
			shard.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size + new Vector3(0, 0, 0.006f) } });
			shard.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = _shardMaterial });

			// Most pieces carry on with the bird; a few spray back out over the harbour.
			bool outward = rng.Randf() < 0.18f;
			Vector3 v = outward
				? new Vector3(rng.RandfRange(-0.8f, 0.8f), rng.RandfRange(-0.3f, 0.8f), -rng.RandfRange(0.4f, 1.4f))
				: push * rng.RandfRange(0.35f, 1.0f) + new Vector3(rng.RandfRange(-1.1f, 1.1f), rng.RandfRange(-0.6f, 1.4f), rng.RandfRange(0f, 0.8f));
			shard.LinearVelocity = v;
			shard.AngularVelocity = new Vector3(rng.RandfRange(-25f, 25f), rng.RandfRange(-25f, 25f), rng.RandfRange(-25f, 25f));
			_shards.AddChild(shard);
		}
	}

	/// <summary>Puts the pane back and clears the pieces (when the shot restarts).</summary>
	public void ResetGlass()
	{
		_shards?.QueueFree();
		_shards = null;
		if (RightArchGlass != null)
			RightArchGlass.Visible = true;
	}

	/// <summary>A plank shutter; <paramref name="dir"/> is which way it spans from its hinge.</summary>
	private Node3D BuildShutter(float hingeX, float dir, float openDegrees)
	{
		var hinge = new Node3D
		{
			Name = dir > 0 ? "ShutterLeft" : "ShutterRight",
			Position = new Vector3(hingeX, SillY + 0.72f, WallFace + 0.03f),
			Rotation = new Vector3(0f, Mathf.DegToRad(openDegrees), 0f),
		};
		AddChild(hinge);

		var planks = Textured("blackened_oak", 1.4f, new Color(0.62f, 0.58f, 0.55f));
		planks.Uv1WorldTriplanar = false; // the texture rides along with the swinging shutter
		var iron = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.065f, 0.06f), Metallic = 0.6f, Roughness = 0.55f };

		const float width = 0.86f, height = 1.44f, plank = 0.21f, gap = 0.008f;
		for (int i = 0; i < 4; i++)
		{
			float x = dir * (plank / 2f + i * (plank + gap) + 0.01f);
			AddBox(hinge, new Vector3(plank, height - (i % 2) * 0.02f, 0.055f), new Vector3(x, 0f, 0f), planks);
		}
		foreach (float y in new[] { -0.48f, 0.48f })
		{
			AddBox(hinge, new Vector3(width * 0.92f, 0.05f, 0.012f), new Vector3(dir * width * 0.46f, y, 0.034f), iron);
			AddBox(hinge, new Vector3(0.05f, 0.09f, 0.07f), new Vector3(0f, y, 0f), iron);
		}
		// Ledge braces on the back.
		foreach (float y in new[] { -0.48f, 0.48f })
			AddBox(hinge, new Vector3(width * 0.9f, 0.1f, 0.035f), new Vector3(dir * width * 0.47f, y, -0.045f), planks);
		return hinge;
	}

	private static void AddBox(Node3D parent, Vector3 size, Vector3 position, Material material)
	{
		parent.AddChild(new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = material,
			Position = position,
		});
	}

	// ---------------------------------------------------------------- props

	private void PlaceProp(string path, Vector3 position, float yawDegrees, string fit, float size, Color tint)
	{
		var scene = GD.Load<PackedScene>(path);
		if (scene == null)
			return;

		var pivot = new Node3D { Name = path.GetBaseDir().GetFile(), Position = position,
			Rotation = new Vector3(0f, Mathf.DegToRad(yawDegrees), 0f) };
		AddChild(pivot);
		var model = scene.Instantiate<Node3D>();
		pivot.AddChild(model);

		Aabb bounds = default;
		bool first = true;
		foreach (var node in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mi = (MeshInstance3D)node;
			Aabb box = RelativeTo(model, mi) * mi.GetAabb();
			bounds = first ? box : bounds.Merge(box);
			first = false;

			// Meshy textures are lit evenly; pull them down to sit in a dim room.
			for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
			{
				if (mi.Mesh.SurfaceGetMaterial(s) is BaseMaterial3D m)
				{
					var dim = (BaseMaterial3D)m.Duplicate();
					dim.AlbedoColor *= tint;
					mi.SetSurfaceOverrideMaterial(s, dim);
				}
			}
		}

		float scale = fit == "w" ? size / bounds.Size.X : size / bounds.Size.Y;
		// Sit on the floor; the hearth also backs onto the window wall.
		var offset = new Vector3(-bounds.GetCenter().X, -bounds.Position.Y, -bounds.GetCenter().Z) * scale;
		model.Transform = new Transform3D(Basis.FromScale(Vector3.One * scale), offset);

		if (path.Contains("hearth"))
			pivot.Position = position with { Z = WallFace + bounds.Size.Z * scale / 2f };
		if (path.Contains("trestle_table"))
		{
			_candleLight = new OmniLight3D
			{
				LightColor = new Color(1f, 0.72f, 0.4f),
				LightEnergy = 0.55f,
				OmniRange = 2.2f,
				ShadowEnabled = true,
				Position = new Vector3(0.6f, bounds.Size.Y * scale * 0.8f, 0f),
			};
			pivot.AddChild(_candleLight);
		}
	}

	private static Transform3D RelativeTo(Node3D root, Node3D node)
	{
		Transform3D t = Transform3D.Identity;
		for (Node n = node; n != null && n != root; n = n.GetParent())
			if (n is Node3D n3) t = n3.Transform * t;
		return t;
	}

	// ---------------------------------------------------------------- fire

	private void BuildFire()
	{
		var hearth = GetNodeOrNull<Node3D>("hearth");
		if (hearth == null)
			return;

		// The firebox sits low and roughly central in the Meshy hearth.
		var fireBase = hearth.Position + new Vector3(0f, 0.14f, -0.1f);
		FirePosition = fireBase;

		_fireLight = new OmniLight3D
		{
			Name = "FireLight",
			LightColor = new Color(1f, 0.55f, 0.26f),
			LightEnergy = 1.5f,
			OmniRange = 3.6f,
			OmniAttenuation = 1.4f,
			ShadowEnabled = true,
			Position = fireBase + new Vector3(0f, 0.35f, 0.35f),
		};
		AddChild(_fireLight);

		// Glowing embers among the logs.
		var rng = new RandomNumberGenerator { Seed = 7 };
		for (int i = 0; i < 14; i++)
		{
			var mat = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.15f, 0.05f, 0.02f),
				EmissionEnabled = true,
				Emission = new Color(1f, rng.RandfRange(0.25f, 0.45f), 0.06f),
				EmissionEnergyMultiplier = 3f,
				Roughness = 1f,
			};
			_embers.Add(mat);
			var ember = new MeshInstance3D
			{
				Mesh = new SphereMesh { Radius = rng.RandfRange(0.025f, 0.05f), Height = rng.RandfRange(0.03f, 0.06f), RadialSegments = 6, Rings = 3 },
				MaterialOverride = mat,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				Position = fireBase + new Vector3(rng.RandfRange(-0.3f, 0.3f), rng.RandfRange(-0.02f, 0.06f), rng.RandfRange(-0.12f, 0.12f)),
			};
			AddChild(ember);
		}

		// Low, lazy flames.
		var flameMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
			BillboardKeepScale = true,
			VertexColorUseAsAlbedo = true,
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
		var ramp = new Gradient
		{
			Offsets = new[] { 0f, 0.35f, 1f },
			Colors = new[] { new Color(1f, 0.75f, 0.3f, 0.9f), new Color(1f, 0.38f, 0.08f, 0.6f), new Color(0.5f, 0.1f, 0.02f, 0f) },
		};
		var shrink = new Curve();
		shrink.AddPoint(new Vector2(0f, 1f));
		shrink.AddPoint(new Vector2(1f, 0.2f));
		AddChild(new CpuParticles3D
		{
			Name = "Flames",
			Amount = 40,
			Lifetime = 0.7,
			LocalCoords = false,
			Position = fireBase + new Vector3(0f, 0.04f, 0f),
			Mesh = new QuadMesh { Size = new Vector2(0.16f, 0.16f), Material = flameMat },
			EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
			EmissionBoxExtents = new Vector3(0.28f, 0.02f, 0.1f),
			Direction = Vector3.Up,
			Spread = 12f,
			InitialVelocityMin = 0.15f,
			InitialVelocityMax = 0.4f,
			Gravity = new Vector3(0f, 0.35f, 0f),
			ScaleAmountMin = 0.6f,
			ScaleAmountMax = 1.3f,
			ScaleAmountCurve = shrink,
			ColorRamp = ramp,
		});
	}
}
