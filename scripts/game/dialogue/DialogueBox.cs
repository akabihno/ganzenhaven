using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// The visual-novel text box: speaker name, typewriter text, choices. Lives in the
/// <see cref="Game"/> autoload so a conversation survives a cutscene in the middle of it.
/// </summary>
/// <remarks>
/// Click, Space or Enter advances; mid-line it reveals the whole line first. Text may use
/// BBCode ([i]…[/i], [b]…[/b], [color=…]).
/// </remarks>
public partial class DialogueBox : CanvasLayer
{
	[Signal] public delegate void FinishedEventHandler();

	public float CharactersPerSecond { get; set; } = 45f;

	public bool IsActive => _runner != null;

	private DialogueRunner _runner;
	private Control _root;
	private PanelContainer _panel;
	private Label _speaker;
	private RichTextLabel _text;
	private Label _more;
	private VBoxContainer _choices;
	private float _shown;
	private bool _typing;
	private bool _busy;          // a wait / cutscene is in progress
	private float _blink;
	private Action<string, string[]> _onGameCommand;

	public override void _Ready()
	{
		Layer = 50;
		BuildUi();
		Hide();
	}

	/// <summary>Starts <paramref name="scriptId"/>. Game-level commands (cutscene, travel, map)
	/// go to <paramref name="onGameCommand"/>; the dialogue waits until <see cref="Resume"/>.</summary>
	public void Start(string scriptId, Action<string, string[]> onGameCommand)
	{
		_onGameCommand = onGameCommand;
		_runner = new DialogueRunner(scriptId, GameState.Instance.Language);
		Game.Instance.CurrentStage?.OnDialogueStarted();
		Show();
		Advance();
	}

	/// <summary>Continues after a game command (cutscene, wait) has finished.</summary>
	public void Resume()
	{
		_busy = false;
		if (_runner != null)
		{
			Show();
			Advance();
		}
	}

	public void Stop()
	{
		if (_runner == null)
			return;
		_runner = null;
		_busy = false;
		Hide();
		Game.Instance.CurrentStage?.OnDialogueEnded();
		EmitSignal(SignalName.Finished);
	}

	// ---------------------------------------------------------------- flow

	private void Advance()
	{
		ClearChoices();
		while (_runner != null && !_busy)
		{
			var ev = _runner.Next();
			switch (ev)
			{
				case DialogueRunner.SayEvent say:
					ShowLine(say.Speaker, say.Text);
					return;

				case DialogueRunner.ChoiceEvent choice:
					ShowChoices(choice.Options);
					return;

				case DialogueRunner.CommandEvent cmd:
					RunCommand(cmd);
					break;

				case DialogueRunner.EndEvent:
					Stop();
					return;
			}
		}
	}

	private void RunCommand(DialogueRunner.CommandEvent cmd)
	{
		var stage = Game.Instance.CurrentStage;
		string Arg(int i) => i < cmd.Args.Length ? cmd.Args[i] : "";
		switch (cmd.Name)
		{
			case "bg":
				stage?.SetBackground(Arg(0));
				break;
			case "show":
				stage?.ShowCharacter(Arg(0), Arg(1), Arg(2));
				break;
			case "hide":
				stage?.HideCharacter(Arg(0));
				break;
			case "wait":
			{
				float seconds = float.TryParse(Arg(0), System.Globalization.NumberStyles.Float,
					System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1f;
				_busy = true;
				_panel.Hide();
				GetTree().CreateTimer(seconds).Timeout += () => { _panel.Show(); Resume(); };
				break;
			}
			case "cutscene":
				_busy = true;
				Hide();
				_onGameCommand?.Invoke(cmd.Name, cmd.Args);
				break;
			case "travel":
			case "map":
				// Leaving ends the conversation; the move happens once the box is closed.
				var args = cmd.Args;
				var name = cmd.Name;
				Stop();
				_onGameCommand?.Invoke(name, args);
				break;
			case "music":
			case "sound":
				GD.Print($"[dialogue] {cmd.Name} {cmd.Rest} (audio not wired up yet)");
				break;
			default:
				GD.PushWarning($"{cmd.Path}:{cmd.Line}: unknown command @{cmd.Name}");
				break;
		}
	}

	// _word_ in a script is shown in italics (BBCode [i] works too).
	private static readonly System.Text.RegularExpressions.Regex Underscored =
		new(@"(?<![\w])_(?<t>[^_\n]+?)_(?![\w])");

	private void ShowLine(string speaker, string text)
	{
		text = Underscored.Replace(text, "[i]${t}[/i]");
		_panel.Show();
		_speaker.Text = speaker;
		_speaker.Visible = speaker.Length > 0;
		_text.Text = speaker.Length > 0 ? text : $"[i]{text}[/i]";
		_text.VisibleCharacters = 0;
		_shown = 0f;
		_typing = true;
		_more.Hide();
	}

	private void ShowChoices(IReadOnlyList<string> options)
	{
		_typing = false;
		_more.Hide();
		for (int i = 0; i < options.Count; i++)
		{
			int index = i;
			var button = new Button
			{
				Text = options[i],
				CustomMinimumSize = new Vector2(760, 64),
				FocusMode = Control.FocusModeEnum.All,
			};
			button.AddThemeFontSizeOverride("font_size", 34);
			button.Pressed += () =>
			{
				_runner?.Choose(index);
				Advance();
			};
			_choices.AddChild(button);
		}
		_choices.Show();
		if (_choices.GetChildCount() > 0)
			((Control)_choices.GetChild(0)).CallDeferred(Control.MethodName.GrabFocus);
	}

	private void ClearChoices()
	{
		foreach (var child in _choices.GetChildren())
			child.QueueFree();
		_choices.Hide();
	}

	public override void _Process(double delta)
	{
		if (_runner == null)
			return;
		if (_typing)
		{
			_shown += (float)delta * CharactersPerSecond;
			_text.VisibleCharacters = (int)_shown;
			if (_shown >= _text.GetTotalCharacterCount())
				FinishTyping();
		}
		else if (_more.Visible || _choices.Visible == false)
		{
			_blink += (float)delta;
			_more.Modulate = new Color(1, 1, 1, 0.45f + 0.45f * Mathf.Sin(_blink * 4f));
		}
	}

	private void FinishTyping()
	{
		_typing = false;
		_text.VisibleCharacters = -1;
		_more.Show();
	}

	private void OnClick()
	{
		if (_runner == null || _busy || _choices.Visible)
			return;
		if (_typing)
			FinishTyping();
		else
			Advance();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (_runner == null)
			return;
		if (e.IsActionPressed("ui_accept") && !_choices.Visible)
		{
			OnClick();
			GetViewport().SetInputAsHandled();
		}
	}

	// ---------------------------------------------------------------- layout

	private void BuildUi()
	{
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Theme = Game.UiTheme };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				OnClick();
		};
		AddChild(_root);

		_panel = new PanelContainer();
		_panel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		_panel.OffsetLeft = 120;
		_panel.OffsetRight = -120;
		_panel.OffsetTop = -300;
		_panel.OffsetBottom = -36;
		_panel.MouseFilter = Control.MouseFilterEnum.Ignore;
		_panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.055f, 0.045f, 0.86f),
			BorderColor = new Color(0.62f, 0.5f, 0.3f, 0.9f),
			BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			ContentMarginLeft = 44, ContentMarginRight = 44, ContentMarginTop = 22, ContentMarginBottom = 22,
			ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 12,
		});
		_root.AddChild(_panel);

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 8);
		_panel.AddChild(column);

		_speaker = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		_speaker.AddThemeFontSizeOverride("font_size", 40);
		_speaker.AddThemeColorOverride("font_color", new Color(0.93f, 0.78f, 0.48f));
		column.AddChild(_speaker);

		_text = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = false,
			ScrollActive = false,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_text.AddThemeFontSizeOverride("normal_font_size", 36);
		_text.AddThemeFontSizeOverride("italics_font_size", 36);
		_text.AddThemeColorOverride("default_color", new Color(0.95f, 0.92f, 0.86f));
		var italic = GD.Load<Font>("res://assets/fonts/EBGaramond-Italic.ttf");
		if (italic != null)
			_text.AddThemeFontOverride("italics_font", italic);
		column.AddChild(_text);

		// Overlaid on the text (a PanelContainer stacks its children), pinned bottom-right.
		_more = new Label
		{
			Text = "▼",
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_more.AddThemeFontSizeOverride("font_size", 26);
		_more.AddThemeColorOverride("font_color", new Color(0.93f, 0.78f, 0.48f));
		_panel.AddChild(_more);

		// Choices stack upward from just above the text panel, so the line they answer stays readable.
		_choices = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		_choices.AddThemeConstantOverride("separation", 14);
		_choices.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		_choices.OffsetLeft = -440;
		_choices.OffsetRight = 440;
		_choices.OffsetTop = -900;
		_choices.OffsetBottom = -330;
		_root.AddChild(_choices);
	}
}
