using Godot;

/// <summary>
/// After the Tavern Keeper's death night falls over everything. Pictures with a night
/// version (&lt;name&gt;_after.png next to &lt;name&gt;.png) switch to it; what has none is tinted
/// moonlit (people, props); the table screens (map, books) get candlelight.
/// </summary>
public static class Night
{
	public static bool IsNight => GameState.Instance?.IsAfter ?? false;

	/// <summary>People standing in a location.</summary>
	public static readonly Color Figure = new(0.5f, 0.54f, 0.72f);
	/// <summary>Someone speaking, up close: still readable, but in the same cold light.</summary>
	public static readonly Color Speaker = new(0.78f, 0.8f, 0.94f);
	/// <summary>Things lying about without a night picture of their own.</summary>
	public static readonly Color Prop = new(0.46f, 0.5f, 0.68f);

	/// <summary>The night version of <paramref name="texture"/> if it's night and one exists.</summary>
	public static Texture2D Variant(Texture2D texture, out bool swapped)
	{
		swapped = false;
		if (texture == null || !IsNight || string.IsNullOrEmpty(texture.ResourcePath))
			return texture;
		string path = texture.ResourcePath;
		if (path.EndsWith("_after.png"))
		{
			swapped = true;
			return texture;
		}
		string night = path.Replace(".png", "_after.png");
		if (!ResourceLoader.Exists(night))
			return texture;
		swapped = true;
		return GD.Load<Texture2D>(night);
	}

	/// <summary>The night version of a picture whatever the act, or the picture itself.</summary>
	public static Texture2D NightOf(Texture2D texture)
	{
		if (texture == null || string.IsNullOrEmpty(texture.ResourcePath) || texture.ResourcePath.EndsWith("_after.png"))
			return texture;
		string night = texture.ResourcePath.Replace(".png", "_after.png");
		return ResourceLoader.Exists(night) ? GD.Load<Texture2D>(night) : texture;
	}

	/// <summary>The day version of a picture (to switch back when the act is flipped in testing).</summary>
	public static Texture2D Day(Texture2D texture)
	{
		if (texture == null || !texture.ResourcePath.EndsWith("_after.png"))
			return texture;
		string day = texture.ResourcePath.Replace("_after.png", ".png");
		return ResourceLoader.Exists(day) ? GD.Load<Texture2D>(day) : texture;
	}

	/// <summary>A full-screen candlelight layer for a table screen; shown only at night.</summary>
	public static ColorRect Candlelight(Vector2 candle)
	{
		var layer = new ColorRect
		{
			Name = "Candlelight",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/candlelight.gdshader") },
			Visible = IsNight,
		};
		((ShaderMaterial)layer.Material).SetShaderParameter("candle", candle);
		layer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		return layer;
	}
}
