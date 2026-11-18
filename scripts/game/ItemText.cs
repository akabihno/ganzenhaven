using Godot;
using System.Collections.Generic;

/// <summary>
/// Inventory item names and descriptions, one file per language: dialogs/&lt;language&gt;/items.txt.
/// Missing translations fall back to English, then to the id. Pictures are
/// assets/art/items/&lt;id&gt;.png.
/// </summary>
/// <remarks>
/// <code>
/// cloth: Piece of cloth
///   A scrap of coarse burlap off a thorn bush.     (indented: the description, optional)
/// </code>
/// </remarks>
public static class ItemText
{
	private sealed class Item
	{
		public string Name = "";
		public string Description = "";
	}

	private static readonly Dictionary<string, Dictionary<string, Item>> Cache = new();

	public static string Name(string id, string language)
	{
		foreach (var lang in Chain(language))
			if (Load(lang).TryGetValue(id, out var item) && item.Name.Length > 0)
				return item.Name;
		return id;
	}

	public static string Description(string id, string language)
	{
		foreach (var lang in Chain(language))
			if (Load(lang).TryGetValue(id, out var item) && item.Description.Length > 0)
				return item.Description;
		return "";
	}

	public static Texture2D Picture(string id)
	{
		string path = $"res://assets/art/items/{id}.png";
		return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
	}

	private static string[] Chain(string language) => language == "en" ? new[] { "en" } : new[] { language, "en" };

	private static Dictionary<string, Item> Load(string language)
	{
		// Re-read in debug builds so edits show without restarting.
		if (!OS.IsDebugBuild() && Cache.TryGetValue(language, out var cached))
			return cached;

		var items = new Dictionary<string, Item>();
		string path = $"res://dialogs/{language}/items.txt";
		if (FileAccess.FileExists(path))
		{
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			Item current = null;
			foreach (var raw in file.GetAsText().TrimStart('﻿').Split('\n'))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith('#'))
					continue;
				bool indented = raw[0] == ' ' || raw[0] == '\t';
				if (indented)
				{
					// Description lines, joined with spaces.
					if (current != null)
						current.Description = current.Description.Length == 0 ? line : $"{current.Description} {line}";
					continue;
				}
				int colon = line.IndexOf(':');
				if (colon <= 0)
					continue;
				current = new Item { Name = line[(colon + 1)..].Trim() };
				items[line[..colon].Trim()] = current;
			}
		}
		Cache[language] = items;
		return items;
	}
}
