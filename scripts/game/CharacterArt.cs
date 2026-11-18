using Godot;

/// <summary>
/// Where character art lives: assets/art/characters/&lt;id&gt;.png, with expressions as
/// &lt;id&gt;_&lt;expression&gt;.png (falling back to the plain sprite when an expression is missing).
/// Display names come from translations/ui.csv as <c>char_&lt;id&gt;</c>.
/// <c>@show</c> also takes an inventory item id (assets/art/items/&lt;id&gt;.png): the item is
/// held up close, above the text box.
/// </summary>
public static class CharacterArt
{
	public static Texture2D Load(string id, string expression = "")
	{
		if (!string.IsNullOrEmpty(expression))
		{
			string withExpression = $"res://assets/art/characters/{id}_{expression}.png";
			if (ResourceLoader.Exists(withExpression))
				return GD.Load<Texture2D>(withExpression);
		}
		string path = $"res://assets/art/characters/{id}.png";
		if (ResourceLoader.Exists(path))
			return GD.Load<Texture2D>(path);
		if (ItemText.Picture(id) is { } item)
			return item;
		GD.PushWarning($"Missing character art {path}");
		return null;
	}

	/// <summary>An inventory item rather than a character.</summary>
	public static bool IsItem(string id) =>
		!ResourceLoader.Exists($"res://assets/art/characters/{id}.png") && ResourceLoader.Exists($"res://assets/art/items/{id}.png");

	/// <summary>
	/// The character a speaker name refers to: matched against the display names in
	/// translations/ui.csv (char_&lt;id&gt;) in the current language, or the id itself.
	/// Empty when nobody matches.
	/// </summary>
	public static string FindIdBySpeaker(string speaker)
	{
		if (string.IsNullOrWhiteSpace(speaker))
			return "";
		string wanted = speaker.Trim();
		foreach (var file in ResourceLoader.ListDirectory("res://assets/art/characters"))
		{
			if (!file.EndsWith(".png"))
				continue;
			string id = file[..^4];
			string key = $"char_{id}";
			string name = TranslationServer.Translate(key);
			// Expression sprites (seagull_tilt) have no display name of their own.
			if (name == key)
				continue;
			if (string.Equals(name, wanted, System.StringComparison.OrdinalIgnoreCase)
				|| string.Equals(id, wanted.Replace(' ', '_'), System.StringComparison.OrdinalIgnoreCase))
				return id;
		}
		return "";
	}

	/// <summary>Where a dialogue sprite's feet go, as a share of the screen height. People
	/// stand behind the text box like in a visual novel; small creatures stand on top of it.</summary>
	public static float DialogueBaseline(string id) => id switch
	{
		"black_cat" or "seagull" => 0.74f,
		_ when IsItem(id) => 0.7f,
		_ => 1.04f,
	};

	/// <summary>Height of a dialogue sprite as a share of the screen height.</summary>
	public static float DialogueScale(string id) => id switch
	{
		"black_cat" => 0.36f,
		"seagull" => 0.42f,
		_ when IsItem(id) => 0.5f,
		_ => 0.96f,
	};
}
