using Godot;
using System.Collections.Generic;

/// <summary>
/// Journal entry texts, one file per language: dialogs/&lt;language&gt;/journal.txt with lines
/// <c>id: text</c> (and <c>#</c> comments). Missing translations fall back to English, then to
/// the id itself, so a new entry shows up even before its text is written.
/// </summary>
/// <remarks>
/// An indented line is a step of the task above it; the journal writes steps under their
/// task, and they go to the "done" page together with it:
/// <code>
/// find_fish: Find fish
///   fish_suspicious: Fish is suspicious
/// </code>
/// </remarks>
public static class JournalText
{
	private sealed record Entry(string Text, string Parent);

	private static readonly Dictionary<string, Dictionary<string, Entry>> Cache = new();

	public static string Get(string id, string language)
	{
		foreach (var lang in Chain(language))
			if (Load(lang).TryGetValue(id, out var entry))
				return entry.Text;
		return id;
	}

	/// <summary>The task <paramref name="id"/> is a step of, or "" for a task of its own.</summary>
	public static string Parent(string id, string language)
	{
		foreach (var lang in Chain(language))
			if (Load(lang).TryGetValue(id, out var entry))
				return entry.Parent;
		return "";
	}

	/// <summary>Ids of the steps written under task <paramref name="id"/>.</summary>
	public static IEnumerable<string> Steps(string id, string language)
	{
		var seen = new HashSet<string>();
		foreach (var lang in Chain(language))
			foreach (var (stepId, entry) in Load(lang))
				if (entry.Parent == id && seen.Add(stepId))
					yield return stepId;
	}

	private static string[] Chain(string language) => language == "en" ? new[] { "en" } : new[] { language, "en" };

	private static Dictionary<string, Entry> Load(string language)
	{
		// Re-read in debug builds so edits show without restarting.
		if (!OS.IsDebugBuild() && Cache.TryGetValue(language, out var cached))
			return cached;

		var entries = new Dictionary<string, Entry>();
		string path = $"res://dialogs/{language}/journal.txt";
		if (FileAccess.FileExists(path))
		{
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			string task = "";
			foreach (var raw in file.GetAsText().TrimStart('﻿').Split('\n'))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith('#'))
					continue;
				int colon = line.IndexOf(':');
				if (colon <= 0)
					continue;
				string id = line[..colon].Trim();
				bool indented = raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t');
				entries[id] = new Entry(line[(colon + 1)..].Trim(), indented ? task : "");
				if (!indented)
					task = id;
			}
		}
		Cache[language] = entries;
		return entries;
	}
}
