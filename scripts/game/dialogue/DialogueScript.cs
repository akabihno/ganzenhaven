using Godot;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// A parsed dialogue script: the plain-text files under dialogs/&lt;language&gt;/.
/// The format is documented in dialogs/README.md.
/// </summary>
/// <remarks>
/// Blocks are made by indentation (spaces; a tab counts as four): the lines under a
/// <c>* choice</c>, <c>@if</c>, <c>@elif</c> or <c>@else</c> that are indented further belong to it.
/// </remarks>
public sealed class DialogueScript
{
	public abstract record Stmt(int Line);
	/// <summary><paramref name="Expression"/>: from "Name [expression]: text", for this line only.</summary>
	public sealed record Say(int Line, string Speaker, string Text, string Expression = "") : Stmt(Line);
	public sealed record Command(int Line, string Name, string[] Args, string Rest) : Stmt(Line);
	/// <summary>A choice option. <paramref name="Leave"/>: picking it closes the menu for good
	/// (written <c>@leave</c> after the option text); otherwise the menu comes back afterwards
	/// without it.</summary>
	public sealed record Option(string Text, string Condition, List<Stmt> Body, bool Leave = false);
	public sealed record Choice(int Line, List<Option> Options) : Stmt(Line);
	public sealed record Branch(string Condition, List<Stmt> Body);
	public sealed record If(int Line, List<Branch> Branches, List<Stmt> Else) : Stmt(Line);

	public string Id { get; }
	public string Path { get; }

	/// <summary>Language of the file actually loaded ("ru", or "en" when it fell back).</summary>
	public string Language { get; }
	public List<Stmt> Body { get; }
	public Dictionary<string, int> Labels { get; } = new();

	private DialogueScript(string id, string path, List<Stmt> body)
	{
		Id = id;
		Path = path;
		// res://dialogs/<lang>/<id>.txt
		var parts = path.Split('/');
		Language = parts.Length > 3 ? parts[3] : "en";
		Body = body;
		for (int i = 0; i < body.Count; i++)
			if (body[i] is Command { Name: "label" } label && label.Args.Length > 0)
				Labels[label.Args[0]] = i;
	}

	// ---------------------------------------------------------------- loading

	private static readonly Dictionary<string, DialogueScript> Cache = new();

	/// <summary>
	/// Loads dialogs/&lt;language&gt;/&lt;id&gt;.txt, falling back to English when the
	/// translation is missing. In debug builds files are re-read every time, so edits show
	/// up the next time the script runs without restarting the game.
	/// </summary>
	public static DialogueScript Load(string id, string language)
	{
		foreach (var lang in language == "en" ? new[] { "en" } : new[] { language, "en" })
		{
			string path = $"res://dialogs/{lang}/{id}.txt";
			if (!FileAccess.FileExists(path))
				continue;
			if (lang != language)
				GD.PushWarning($"Dialogue '{id}' has no {language} version yet; using {lang}.");

			if (!OS.IsDebugBuild() && Cache.TryGetValue(path, out var cached))
				return cached;
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			var script = Parse(id, path, file.GetAsText());
			Cache[path] = script;
			return script;
		}
		GD.PushError($"Dialogue script not found: dialogs/{language}/{id}.txt");
		return null;
	}

	// ---------------------------------------------------------------- parsing

	private readonly record struct SourceLine(int Number, int Indent, string Text);

	private static string StripLeave(string text, ref bool leave)
	{
		if (text != "@leave" && !text.EndsWith(" @leave", System.StringComparison.Ordinal))
			return text;
		leave = true;
		return text[..^"@leave".Length].Trim();
	}

	// "Name: text" or "Name [expression]: text". The name may not hold sentence punctuation, so
	// "Note: ..." style narration can still be forced with a leading backslash.
	private static readonly Regex SpeakerLine = new(@"^(?<name>[^\s:@$*\\#\[][^:.!?""«»\[]{0,40}?)(\s*\[(?<expr>[\w-]+)\])?:\s+(?<text>.+)$");

	public static DialogueScript Parse(string id, string path, string source)
	{
		var lines = new List<SourceLine>();
		var raw = source.Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('﻿').Split('\n');
		for (int i = 0; i < raw.Length; i++)
		{
			string line = raw[i].TrimEnd();
			string trimmed = line.TrimStart();
			if (trimmed.Length == 0 || trimmed.StartsWith('#'))
				continue;
			int indent = 0;
			foreach (char c in line)
			{
				if (c == ' ') indent++;
				else if (c == '\t') indent += 4;
				else break;
			}
			lines.Add(new SourceLine(i + 1, indent, trimmed));
		}

		int pos = 0;
		var body = ParseBlock(lines, ref pos, lines.Count > 0 ? lines[0].Indent : 0, path);
		return new DialogueScript(id, path, body);
	}

	private static List<Stmt> ParseBlock(List<SourceLine> lines, ref int pos, int indent, string path)
	{
		var block = new List<Stmt>();
		while (pos < lines.Count && lines[pos].Indent >= indent)
		{
			var line = lines[pos];
			if (line.Indent > indent)
			{
				GD.PushWarning($"{path}:{line.Number}: unexpected indentation");
				block.AddRange(ParseBlock(lines, ref pos, line.Indent, path));
				continue;
			}

			string text = line.Text;
			if (text.StartsWith("* "))
			{
				var options = new List<Option>();
				while (pos < lines.Count && lines[pos].Indent == indent && lines[pos].Text.StartsWith("* "))
				{
					string optionText = lines[pos].Text[2..].Trim();
					string condition = "";
					int at = optionText.LastIndexOf(" @if ", System.StringComparison.Ordinal);
					if (at >= 0)
					{
						condition = optionText[(at + 5)..].Trim();
						optionText = optionText[..at].Trim();
					}
					// "@leave" may come before or after "@if condition".
					bool leave = false;
					optionText = StripLeave(optionText, ref leave);
					condition = StripLeave(condition, ref leave);
					pos++;
					options.Add(new Option(optionText, condition, ChildBlock(lines, ref pos, indent, path), leave));
				}
				block.Add(new Choice(line.Number, options));
				continue;
			}

			if (text.StartsWith("@if "))
			{
				var branches = new List<Branch>();
				List<Stmt> elseBody = null;
				pos++;
				branches.Add(new Branch(text[4..].Trim(), ChildBlock(lines, ref pos, indent, path)));
				while (pos < lines.Count && lines[pos].Indent == indent)
				{
					string next = lines[pos].Text;
					if (next.StartsWith("@elif "))
					{
						pos++;
						branches.Add(new Branch(next[6..].Trim(), ChildBlock(lines, ref pos, indent, path)));
					}
					else if (next == "@else")
					{
						pos++;
						elseBody = ChildBlock(lines, ref pos, indent, path);
						break;
					}
					else break;
				}
				block.Add(new If(line.Number, branches, elseBody ?? new List<Stmt>()));
				continue;
			}

			pos++;
			if (text.StartsWith('@') || text.StartsWith('$'))
			{
				// "@show fisherman worried" -> name "show"; "$set flag" -> name "$set".
				string head = text.StartsWith('@') ? text[1..] : text;
				int space = head.IndexOf(' ');
				string name = space < 0 ? head : head[..space];
				string rest = space < 0 ? "" : head[(space + 1)..].Trim();
				var args = rest.Length == 0 ? System.Array.Empty<string>() : rest.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
				block.Add(new Command(line.Number, name, args, rest));
			}
			else if (text.StartsWith('\\'))
			{
				block.Add(new Say(line.Number, "", text[1..].TrimStart()));
			}
			else
			{
				var m = SpeakerLine.Match(text);
				block.Add(m.Success
					? new Say(line.Number, m.Groups["name"].Value.Trim(), m.Groups["text"].Value.Trim(), m.Groups["expr"].Value)
					: new Say(line.Number, "", text));
			}
		}
		return block;
	}

	private static List<Stmt> ChildBlock(List<SourceLine> lines, ref int pos, int parentIndent, string path)
	{
		if (pos < lines.Count && lines[pos].Indent > parentIndent)
			return ParseBlock(lines, ref pos, lines[pos].Indent, path);
		return new List<Stmt>();
	}
}
