using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Story state for one playthrough: flags, numeric variables and where the player is.
/// Autoloaded as <c>GameState</c>.
/// </summary>
/// <remarks>
/// <para>Dialogue scripts read and write it through <c>$set</c>, <c>$unset</c>, <c>$add</c> and
/// <c>@if</c>. The game is split in two acts by one flag, <see cref="TavernKeeperDead"/>:
/// before it people behave more or less sanely, after it the harbour comes apart.</para>
/// <para>Also holds the journal: entries a script adds with <c>$journal id</c> and ticks off
/// with <c>$journal done id</c>; their text lives in dialogs/&lt;language&gt;/journal.txt.</para>
/// <para>Saved as JSON in user://save.json; the language choice is kept separately in
/// user://settings.cfg so it survives a new game.</para>
/// </remarks>
public partial class GameState : Node
{
	public const string TavernKeeperDead = "tavern_keeper_dead";

	private const string SavePath = "user://save.json";
	private const string SettingsPath = "user://settings.cfg";

	[Signal] public delegate void ChangedEventHandler();
	[Signal] public delegate void LanguageChangedEventHandler(string language);
	/// <summary>A journal entry was added (<paramref name="done"/> false) or completed (true).</summary>
	[Signal] public delegate void JournalUpdatedEventHandler(string id, bool done);

	public static GameState Instance { get; private set; }

	private readonly HashSet<string> _flags = new();
	private readonly Dictionary<string, double> _vars = new();
	private readonly List<JournalEntry> _journal = new();

	public sealed record JournalEntry(string Id, bool Done);

	/// <summary>Journal entries in the order they were added.</summary>
	public IReadOnlyList<JournalEntry> Journal => _journal;

	/// <summary>Location id the player is at (also where Continue takes them).</summary>
	public string Location { get; set; } = "office";

	/// <summary>Second half of the game: after the Tavern Keeper's death.</summary>
	public bool IsAfter => HasFlag(TavernKeeperDead);

	/// <summary>"en" or "ru". Also the dialogs/ subfolder scripts are read from.</summary>
	public string Language { get; private set; } = "en";

	public static readonly string[] Languages = { "en", "ru" };

	public override void _EnterTree()
	{
		Instance = this;
		LoadSettings();
	}

	// ---------------------------------------------------------------- flags and variables

	public bool HasFlag(string flag) => _flags.Contains(flag);

	public void SetFlag(string flag, bool on = true)
	{
		bool changed = on ? _flags.Add(flag) : _flags.Remove(flag);
		if (changed)
			EmitSignal(SignalName.Changed);
	}

	public double GetVar(string name) => _vars.TryGetValue(name, out var v) ? v : 0;

	public void SetVar(string name, double value)
	{
		_vars[name] = value;
		EmitSignal(SignalName.Changed);
	}

	public IEnumerable<string> Flags => _flags;

	/// <summary>
	/// Evaluates a condition as written in scripts and in the editor's ShowIf / HideIf
	/// fields: <c>flag</c>, <c>!flag</c>, <c>name &gt;= 3</c>, <c>journal:id</c> (entry open),
	/// <c>done:id</c> (entry ticked off), joined with <c>and</c> / <c>or</c>
	/// (<c>and</c> binds tighter). An empty condition is true.
	/// </summary>
	public bool Check(string condition)
	{
		if (string.IsNullOrWhiteSpace(condition))
			return true;

		foreach (var alternative in condition.Split(" or "))
		{
			bool all = true;
			foreach (var term in alternative.Split(" and "))
			{
				if (!CheckTerm(term.Trim()))
				{
					all = false;
					break;
				}
			}
			if (all)
				return true;
		}
		return false;
	}

	private bool CheckTerm(string term)
	{
		foreach (var op in new[] { ">=", "<=", "==", "!=", ">", "<" })
		{
			int i = term.IndexOf(op, System.StringComparison.Ordinal);
			if (i < 0)
				continue;
			double left = GetVar(term[..i].Trim());
			if (!double.TryParse(term[(i + op.Length)..].Trim(), System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out double right))
			{
				GD.PushWarning($"Condition: '{term}' does not compare against a number");
				return false;
			}
			return op switch
			{
				">=" => left >= right,
				"<=" => left <= right,
				"==" => left == right,
				"!=" => left != right,
				">" => left > right,
				_ => left < right,
			};
		}

		bool negate = term.StartsWith('!');
		string name = negate ? term[1..].Trim() : term;
		bool result;
		if (name.StartsWith("journal:"))
			result = _journal.Exists(e => e.Id == name[8..].Trim() && !e.Done);
		else if (name.StartsWith("done:"))
			result = _journal.Exists(e => e.Id == name[5..].Trim() && e.Done);
		else
			result = HasFlag(name);
		return negate ? !result : result;
	}

	// ---------------------------------------------------------------- journal

	public void JournalAdd(string id)
	{
		if (string.IsNullOrEmpty(id) || _journal.Exists(e => e.Id == id))
			return;
		_journal.Add(new JournalEntry(id, false));
		EmitSignal(SignalName.JournalUpdated, id, false);
		EmitSignal(SignalName.Changed);
	}

	/// <summary>Ticks an entry off (adding it first if the script never added it).</summary>
	public void JournalComplete(string id)
	{
		int i = _journal.FindIndex(e => e.Id == id);
		if (i >= 0 && _journal[i].Done)
			return;
		if (i >= 0)
			_journal[i] = _journal[i] with { Done = true };
		else
			_journal.Add(new JournalEntry(id, true));
		EmitSignal(SignalName.JournalUpdated, id, true);
		EmitSignal(SignalName.Changed);
	}

	public void JournalRemove(string id)
	{
		if (_journal.RemoveAll(e => e.Id == id) > 0)
			EmitSignal(SignalName.Changed);
	}

	// ---------------------------------------------------------------- language

	public void SetLanguage(string language)
	{
		if (System.Array.IndexOf(Languages, language) < 0)
			language = "en";
		Language = language;
		TranslationServer.SetLocale(language);
		var cfg = new ConfigFile();
		cfg.Load(SettingsPath);
		cfg.SetValue("game", "language", language);
		cfg.Save(SettingsPath);
		EmitSignal(SignalName.LanguageChanged, language);
	}

	private void LoadSettings()
	{
		var cfg = new ConfigFile();
		string lang = cfg.Load(SettingsPath) == Error.Ok
			? (string)cfg.GetValue("game", "language", "")
			: "";
		if (string.IsNullOrEmpty(lang))
			lang = OS.GetLocaleLanguage() == "ru" ? "ru" : "en";
		Language = lang;
		TranslationServer.SetLocale(lang);
	}

	// ---------------------------------------------------------------- save / load

	public void Reset()
	{
		_flags.Clear();
		_vars.Clear();
		_journal.Clear();
		Location = "office";
		EmitSignal(SignalName.Changed);
	}

	public static bool HasSave => FileAccess.FileExists(SavePath);

	private record SaveData(string Location, List<string> Flags, Dictionary<string, double> Vars, List<JournalEntry> Journal);

	public void Save()
	{
		var data = new SaveData(Location, new List<string>(_flags), new Dictionary<string, double>(_vars), new List<JournalEntry>(_journal));
		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
		file?.StoreString(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
	}

	public bool Load()
	{
		if (!HasSave)
			return false;
		using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
		var data = JsonSerializer.Deserialize<SaveData>(file.GetAsText());
		if (data == null)
			return false;
		_flags.Clear();
		_flags.UnionWith(data.Flags ?? new List<string>());
		_vars.Clear();
		foreach (var (k, v) in data.Vars ?? new Dictionary<string, double>())
			_vars[k] = v;
		_journal.Clear();
		_journal.AddRange(data.Journal ?? new List<JournalEntry>());
		Location = string.IsNullOrEmpty(data.Location) ? "office" : data.Location;
		EmitSignal(SignalName.Changed);
		return true;
	}
}
