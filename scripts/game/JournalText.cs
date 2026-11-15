using Godot;
using System.Collections.Generic;

/// <summary>
/// Journal entry texts, one file per language: dialogs/&lt;language&gt;/journal.txt with lines
/// <c>id: text</c> (and <c>#</c> comments). Missing translations fall back to English, then to
/// the id itself, so a new entry shows up even before its text is written.
/// </summary>
public static class JournalText
{
	private static readonly Dictionary<string, Dictionary<string, string>> Cache = new();

	public static string Get(string id, string language)
	{
		foreach (var lang in language == "en" ? new[] { "en" } : new[] { language, "en" })
			if (Load(lang).TryGetValue(id, out var text))
				return text;
		return id;
	}

	private static Dictionary<string, string> Load(string language)
	{
		// Re-read in debug builds so edits show without restarting.
		if (!OS.IsDebugBuild() && Cache.TryGetValue(language, out var cached))
			return cached;

		var entries = new Dictionary<string, string>();
		string path = $"res://dialogs/{language}/journal.txt";
		if (FileAccess.FileExists(path))
		{
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			foreach (var raw in file.GetAsText().Split('\n'))
			{
				string line = raw.Trim().TrimStart('﻿');
				if (line.Length == 0 || line.StartsWith('#'))
					continue;
				int colon = line.IndexOf(':');
				if (colon > 0)
					entries[line[..colon].Trim()] = line[(colon + 1)..].Trim();
			}
		}
		Cache[language] = entries;
		return entries;
	}
}
