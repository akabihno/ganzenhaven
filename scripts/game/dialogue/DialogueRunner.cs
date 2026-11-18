using Godot;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static DialogueScript;

/// <summary>
/// Steps through a <see cref="DialogueScript"/>, keeping story logic (flags, conditions,
/// jumps, calls) to itself and handing everything the player sees to the caller as events.
/// </summary>
/// <remarks>
/// A choice menu comes back after the picked option's lines have run, without that option and
/// with "That's all." added to move on; once every option has been picked, the conversation
/// carries on after the menu. An option marked <c>@leave</c> closes the menu instead (decisions:
/// pay or not, take or leave).
/// </remarks>
public sealed class DialogueRunner
{
	public abstract record Event;
	/// <summary><paramref name="VoicePath"/>: where this line's recording lives if it has been voiced.</summary>
	public sealed record SayEvent(string Speaker, string Text, string Expression = "", string VoicePath = "") : Event;
	public sealed record ChoiceEvent(IReadOnlyList<string> Options) : Event;
	/// <summary>A command for the stage or the game: bg, show, hide, wait, cutscene, travel, map, music, sound.</summary>
	public sealed record CommandEvent(string Name, string[] Args, string Rest, int Line, string Path) : Event;
	public sealed record EndEvent : Event;

	private sealed class Frame
	{
		public List<Stmt> Block;
		public int Index;
		public DialogueScript Script;
		public bool IsScriptRoot;

		// A choice menu waiting on the stack: shown again whenever it comes back to the top.
		public Choice Menu;
		public HashSet<Option> Picked;
		public bool ShownBefore;
	}

	private readonly Stack<Frame> _frames = new();
	private readonly string _language;
	private List<Option> _pendingOptions;
	private bool _pendingDone;   // the last entry of the menu on screen is "That's all."
	private int _guard;

	public DialogueRunner(string scriptId, string language)
	{
		_language = language;
		Enter(scriptId);
	}

	public bool IsWaitingForChoice => _pendingOptions != null;

	/// <summary>Picks option <paramref name="index"/> of the last <see cref="ChoiceEvent"/>.</summary>
	public void Choose(int index)
	{
		if (_pendingOptions == null || index < 0 || index >= _pendingOptions.Count + (_pendingDone ? 1 : 0))
			return;
		var menu = _frames.Peek();
		bool done = index == _pendingOptions.Count;
		var option = done ? null : _pendingOptions[index];
		_pendingOptions = null;

		if (done || option.Leave)
			_frames.Pop();   // the menu is finished; carry on after it
		else
			menu.Picked.Add(option);
		if (option != null && option.Body.Count > 0)
			_frames.Push(new Frame { Block = option.Body, Script = menu.Script });
	}

	/// <summary>Runs until something needs the player or the stage.</summary>
	public Event Next()
	{
		if (_pendingOptions != null)
			return MenuEvent();

		_guard = 0;
		while (_frames.Count > 0)
		{
			if (++_guard > 10000)
			{
				GD.PushError("Dialogue: stopped after 10000 steps without output (an @goto loop?)");
				return new EndEvent();
			}

			var frame = _frames.Peek();
			if (frame.Menu != null)
			{
				var left = frame.Menu.Options.FindAll(o => !frame.Picked.Contains(o) && GameState.Instance.Check(o.Condition));
				if (left.Count == 0)
				{
					_frames.Pop();
					continue;
				}
				_pendingOptions = left;
				_pendingDone = frame.ShownBefore;   // the first time, one of them must be picked
				frame.ShownBefore = true;
				return MenuEvent();
			}
			if (frame.Index >= frame.Block.Count)
			{
				_frames.Pop();
				continue;
			}

			var stmt = frame.Block[frame.Index++];
			switch (stmt)
			{
				case Say say:
					return new SayEvent(Interpolate(say.Speaker), Interpolate(say.Text), say.Expression,
						$"res://audio/voice/{frame.Script.Language}/{frame.Script.Id}/{VoiceKey(say.Speaker, say.Text)}.mp3");

				case Choice choice:
					_frames.Push(new Frame { Block = new List<Stmt>(), Script = frame.Script, Menu = choice, Picked = new HashSet<Option>() });
					continue;

				case If cond:
				{
					List<Stmt> body = cond.Else;
					foreach (var branch in cond.Branches)
					{
						if (GameState.Instance.Check(branch.Condition))
						{
							body = branch.Body;
							break;
						}
					}
					if (body.Count > 0)
						_frames.Push(new Frame { Block = body, Script = frame.Script });
					continue;
				}

				case Command cmd:
				{
					var ev = Execute(cmd, frame);
					if (ev != null)
						return ev;
					continue;
				}
			}
		}
		return new EndEvent();
	}

	private ChoiceEvent MenuEvent()
	{
		var texts = _pendingOptions.ConvertAll(o => Interpolate(o.Text));
		if (_pendingDone)
			texts.Add(TranslationServer.Translate("choice_done").ToString());
		return new ChoiceEvent(texts);
	}

	private Event Execute(Command cmd, Frame frame)
	{
		var state = GameState.Instance;
		switch (cmd.Name)
		{
			case "$set":
			{
				// "$set flag" or "$set name = 3"
				int eq = cmd.Rest.IndexOf('=');
				if (eq < 0)
					state.SetFlag(cmd.Rest.Trim());
				else if (double.TryParse(cmd.Rest[(eq + 1)..].Trim(), System.Globalization.NumberStyles.Float,
							 System.Globalization.CultureInfo.InvariantCulture, out double v))
					state.SetVar(cmd.Rest[..eq].Trim(), v);
				else
					Warn(cmd, frame, "$set name = <number>");
				return null;
			}
			case "$unset":
				state.SetFlag(cmd.Rest.Trim(), false);
				return null;
			case "$add":
			{
				// "$add name 1" (or a negative number)
				if (cmd.Args.Length == 2 && double.TryParse(cmd.Args[1], System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out double d))
					state.SetVar(cmd.Args[0], state.GetVar(cmd.Args[0]) + d);
				else
					Warn(cmd, frame, "$add name <number>");
				return null;
			}
			case "$journal":
			{
				// "$journal id", "$journal done id", "$journal remove id"
				string verb = cmd.Args.Length > 1 ? cmd.Args[0] : "add";
				string id = cmd.Args.Length > 0 ? cmd.Args[^1] : "";
				if (verb == "done") state.JournalComplete(id);
				else if (verb == "remove") state.JournalRemove(id);
				else if (verb == "add") state.JournalAdd(id);
				else Warn(cmd, frame, "$journal id | $journal done id | $journal remove id");
				return null;
			}
			case "$item":
			{
				// "$item id" puts it in the inventory, "$item remove id" takes it out
				string verb = cmd.Args.Length > 1 ? cmd.Args[0] : "add";
				string id = cmd.Args.Length > 0 ? cmd.Args[^1] : "";
				if (verb == "remove") state.ItemRemove(id);
				else if (verb == "add") state.ItemAdd(id);
				else Warn(cmd, frame, "$item id | $item remove id");
				return null;
			}
			case "label":
				return null;
			case "goto":
			{
				string label = cmd.Args.Length > 0 ? cmd.Args[0] : "";
				while (_frames.Count > 0 && !_frames.Peek().IsScriptRoot)
					_frames.Pop();
				var root = _frames.Peek();
				if (root.Script.Labels.TryGetValue(label, out int index))
					root.Index = index;
				else
					Warn(cmd, frame, $"no top-level @label {label}");
				return null;
			}
			case "call":
				Enter(cmd.Rest.Trim());
				return null;
			case "jump":
				_frames.Clear();
				Enter(cmd.Rest.Trim());
				return null;
			case "end":
				_frames.Clear();
				return new EndEvent();
			default:
				return new CommandEvent(cmd.Name, cmd.Args, cmd.Rest, cmd.Line, frame.Script.Path);
		}
	}

	private void Enter(string scriptId)
	{
		var script = DialogueScript.Load(scriptId, _language);
		if (script != null)
			_frames.Push(new Frame { Block = script.Body, Script = script, IsScriptRoot = true });
	}

	private static void Warn(Command cmd, Frame frame, string message) =>
		GD.PushWarning($"{frame.Script.Path}:{cmd.Line}: {message}");

	/// <summary>
	/// Names a line's recording: the first 12 hex digits of sha1("speaker|text") over the line
	/// as written in the script. tools/elevenlabs_voice.py computes the same.
	/// </summary>
	public static string VoiceKey(string speaker, string text)
	{
		byte[] hash = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes($"{speaker}|{text}"));
		return System.Convert.ToHexString(hash).ToLowerInvariant()[..12];
	}

	private static readonly Regex VarRef = new(@"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}");

	/// <summary>Replaces {name} with the variable's value.</summary>
	private static string Interpolate(string text) =>
		VarRef.Replace(text, m => GameState.Instance.GetVar(m.Groups["name"].Value)
			.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
