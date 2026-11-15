using Godot;

/// <summary>
/// Moored ships out in the harbour, seen through the office window. Built from primitives
/// at load time: they sit far enough into the fog that only the masts, yards and rigging
/// read, so a hull is just a dark block.
/// </summary>
/// <remarks>
/// [Tool] so the ships also show in the editor. The generated nodes have no owner, so they
/// are never written into the .tscn.
/// </remarks>
[Tool]
[GlobalClass]
public partial class FoggyHarbourShips : Node3D
{
	[Export] public float WaterLevel { get; set; } = 0.85f;

	private readonly record struct Ship(Vector3 Position, float YawDegrees, float Length, float[] MastOffsets, float[] MastHeights);

	// Ships lie along the quay (local X). Distances are chosen against the fog depth range,
	// so the nearest still has a readable hull and the farthest is just mast tips.
	private static readonly Ship[] Ships =
	{
		new(new Vector3(-5.5f, 0f, -19f), 9f, 14f, new[] { -3.8f, 2.6f }, new[] { 14f, 17f }),
		new(new Vector3(6.5f, 0f, -27f), -7f, 13f, new[] { -3f, 3.2f }, new[] { 16.5f, 12.5f }),
		new(new Vector3(-0.5f, 0f, -37f), 4f, 17f, new[] { -5f, 0.5f, 5.5f }, new[] { 12f, 19f, 15f }),
		new(new Vector3(-13f, 0f, -31f), 15f, 10f, new[] { 0.5f }, new[] { 13f }),
		new(new Vector3(14f, 0f, -44f), -3f, 13f, new[] { -3f, 3f }, new[] { 17f, 14f }),
	};

	private StandardMaterial3D _hull, _wood, _sail, _rope;

	public override void _Ready()
	{
		_hull = Material(new Color(0.16f, 0.11f, 0.07f));
		_wood = Material(new Color(0.13f, 0.09f, 0.06f));
		_sail = Material(new Color(0.82f, 0.79f, 0.70f));
		_rope = Material(new Color(0.13f, 0.11f, 0.09f));

		foreach (var ship in Ships)
			BuildShip(ship);
	}

	private void BuildShip(Ship s)
	{
		var root = new Node3D
		{
			Name = "Ship",
			Position = s.Position,
			Rotation = new Vector3(0f, Mathf.DegToRad(s.YawDegrees), 0f),
		};
		AddChild(root);

		float deckY = WaterLevel + 0.6f;
		const float beam = 3.4f;

		// Hull: a squashed cylinder along the ship, half sunk, so the silhouette is rounded
		// rather than a block.
		var hull = new CylinderMesh { TopRadius = beam / 2f, BottomRadius = beam / 2f, Height = s.Length, RadialSegments = 20 };
		var lie = new Basis(Vector3.Back, Mathf.Pi / 2f) * Basis.FromScale(new Vector3(0.62f, 1f, 1f));
		AddMesh(root, hull, new Vector3(0f, WaterLevel - 0.25f, 0f), lie, _hull);
		// Bow and stern taper in to points.
		var cone = new CylinderMesh { TopRadius = 0.05f, BottomRadius = beam / 2f, Height = 2.6f, RadialSegments = 20 };
		// (The cone's narrow end is its local +Y; -90° about Z points that along +X.)
		AddMesh(root, cone, new Vector3(s.Length / 2f + 1.3f, WaterLevel - 0.25f, 0f),
			new Basis(Vector3.Back, -Mathf.Pi / 2f) * Basis.FromScale(new Vector3(0.62f, 1f, 1f)), _hull);
		AddMesh(root, cone, new Vector3(-s.Length / 2f - 0.6f, WaterLevel - 0.25f, 0f),
			new Basis(Vector3.Back, Mathf.Pi / 2f) * Basis.FromScale(new Vector3(0.62f, 0.5f, 1f)), _hull);
		Box(root, new Vector3(2.2f, 0.9f, beam + 0.2f), new Vector3(s.Length / 2f - 1.1f, deckY + 0.45f, 0f), _hull);
		Box(root, new Vector3(3.0f, 1.3f, beam + 0.2f), new Vector3(-s.Length / 2f + 1.5f, deckY + 0.65f, 0f), _hull);
		// Bowsprit: the forestay runs out to its tip.
		var bowTip = new Vector3(s.Length / 2f + 3.2f, deckY + 1.6f, 0f);
		Line(root, new Vector3(s.Length / 2f - 0.5f, deckY + 0.5f, 0f), bowTip, 0.12f, _wood);
		var sternTop = new Vector3(-s.Length / 2f + 0.3f, deckY + 1.3f, 0f);

		Vector3? previousTop = null;
		for (int i = 0; i < s.MastOffsets.Length; i++)
		{
			float x = s.MastOffsets[i];
			float h = s.MastHeights[i];
			var top = new Vector3(x, deckY + h, 0f);

			var mast = new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.32f, Height = h, RadialSegments = 10 };
			AddMesh(root, mast, new Vector3(x, deckY + h / 2f, 0f), Basis.Identity, _wood);

			var nest = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.45f, Height = 0.45f, RadialSegments = 12 };
			AddMesh(root, nest, new Vector3(x, deckY + h * 0.72f, 0f), Basis.Identity, _wood);

			// Yards lie across the ship; the lower one carries a furled sail.
			var across = new Basis(Vector3.Right, Mathf.Pi / 2f);
			AddMesh(root, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = h * 0.55f, RadialSegments = 8 },
				new Vector3(x, deckY + h * 0.45f, 0f), across, _wood);
			AddMesh(root, new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.28f, Height = h * 0.48f, RadialSegments = 10 },
				new Vector3(x, deckY + h * 0.45f - 0.2f, 0f), across, _sail);
			AddMesh(root, new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = h * 0.38f, RadialSegments = 8 },
				new Vector3(x, deckY + h * 0.82f, 0f), across, _wood);

			// Shrouds, four per side, from the masthead down to the rail.
			for (int side = -1; side <= 1; side += 2)
			for (int k = 0; k < 4; k++)
			{
				var from = new Vector3(x, deckY + h * 0.7f, side * 0.15f);
				var to = new Vector3(x - 1.2f + k * 0.8f, deckY, side * beam / 2f);
				Line(root, from, to, 0.035f, _rope);
			}

			if (previousTop is { } prev)
				Line(root, prev, top + new Vector3(0f, -h * 0.1f, 0f), 0.03f, _rope);
			else
				Line(root, sternTop, top, 0.03f, _rope);
			previousTop = top;

			// A pennant at the masthead.
			Box(root, new Vector3(0.9f, 0.18f, 0.02f), top + new Vector3(0.5f, -0.2f, 0f), _sail);
		}

		if (previousTop is { } last)
			Line(root, last, bowTip, 0.03f, _rope);
	}

	private static StandardMaterial3D Material(Color c) => new()
	{
		AlbedoColor = c,
		Roughness = 0.9f,
	};

	private void Box(Node3D parent, Vector3 size, Vector3 position, Material material)
		=> AddMesh(parent, new BoxMesh { Size = size }, position, Basis.Identity, material);

	private static void AddMesh(Node3D parent, Mesh mesh, Vector3 position, Basis basis, Material material)
	{
		parent.AddChild(new MeshInstance3D
		{
			Mesh = mesh,
			MaterialOverride = material,
			Transform = new Transform3D(basis, position),
		});
	}

	/// <summary>A thin cylinder from <paramref name="a"/> to <paramref name="b"/> (ropes, spars).</summary>
	private static void Line(Node3D parent, Vector3 a, Vector3 b, float radius, Material material)
	{
		Vector3 d = b - a;
		Vector3 y = d.Normalized();
		Vector3 x = y.Cross(Mathf.Abs(y.Y) < 0.99f ? Vector3.Up : Vector3.Right).Normalized();
		Vector3 z = x.Cross(y);
		var mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = d.Length(), RadialSegments = 6, Rings = 1 };
		AddMesh(parent, mesh, (a + b) / 2f, new Basis(x, y, z), material);
	}
}
