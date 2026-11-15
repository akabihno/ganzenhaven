using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Screen flow for the whole game: main menu, map, locations, 3D cutscenes, with fades in
/// between, plus the shared dialogue box and theme. Autoloaded as <c>Game</c>.
/// </summary>
/// <remarks>
/// <para>Locations are scenes at scenes/game/locations/&lt;id&gt;.tscn; cutscenes are listed in
/// <see cref="Cutscenes"/>. A cutscene scene ends by emitting a <c>Finished</c> signal (or
/// the player presses Esc), then the game goes back to where it was.</para>
/// <para>Debug builds only: F9 flips the act (sets / clears tavern_keeper_dead) and reloads the
/// current screen, F10 opens the map from anywhere.</para>
/// </remarks>
public partial class Game : Node
{
	public static Game Instance { get; private set; }

	/// <summary>Cutscene id (as used by <c>@cutscene id</c>) to scene.</summary>
	public static readonly Dictionary<string, string> Cutscenes = new()
	{
		["seagull_arrival"] = "res://scenes/seagull_crash.tscn",
		["funeral"] = "res://scenes/cutscenes/funeral.tscn",
	};

	private const string MenuScene = "res://scenes/game/main_menu.tscn";
	private const string MapScene = "res://scenes/game/map.tscn";

	/// <summary>The game's look (EB Garamond, dark panels, gold accents). Controls under a
	/// CanvasLayer don't inherit the window theme, so they set this themselves.</summary>
	public static Theme UiTheme { get; private set; }

	public IStage CurrentStage { get; private set; }
	public DialogueBox Dialogue { get; private set; }
	public GameHud Hud { get; private set; }

	private CanvasLayer _fadeLayer;
	private ColorRect _fade;
	private bool _switching;
	private Node _cutscene;
	private string _returnScene;

	public override void _EnterTree() => Instance = this;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		UiTheme = BuildTheme();
		GetTree().Root.Theme = UiTheme;

		Dialogue = new DialogueBox { Name = "DialogueBox" };
		AddChild(Dialogue);
		// Whatever a conversation changed (flags, journal) is kept even without travelling.
		Dialogue.Finished += () =>
		{
			if (CurrentStage != null)
				GameState.Instance.Save();
		};

		Hud = new GameHud { Name = "Hud" };
		AddChild(Hud);

		_fadeLayer = new CanvasLayer { Layer = 100 };
		AddChild(_fadeLayer);
		_fade = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
		_fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_fade.Modulate = new Color(1, 1, 1, 0);
		_fadeLayer.AddChild(_fade);
	}

	// ---------------------------------------------------------------- stage registration

	public void RegisterStage(IStage stage) => CurrentStage = stage;

	public void UnregisterStage(IStage stage)
	{
		if (CurrentStage == stage)
			CurrentStage = null;
	}

	// ---------------------------------------------------------------- navigation

	public void NewGame()
	{
		GameState.Instance.Reset();
		GoToLocation("office");
	}

	public void ContinueGame()
	{
		if (!GameState.Instance.Load())
		{
			NewGame();
			return;
		}
		GoToLocation(GameState.Instance.Location);
	}

	public void GoToMenu()
	{
		if (GetTree().CurrentScene is LocationScreen or MapScreen)
			GameState.Instance.Save();
		SwitchTo(MenuScene);
	}

	public void GoToMap() => SwitchTo(MapScene);

	public void GoToLocation(string id)
	{
		string path = $"res://scenes/game/locations/{id}.tscn";
		if (!ResourceLoader.Exists(path))
		{
			GD.PushError($"No location scene for '{id}' ({path})");
			return;
		}
		GameState.Instance.Location = id;
		GameState.Instance.Save();
		SwitchTo(path);
	}

	/// <summary>Runs a dialogue script on the current stage.</summary>
	public void RunDialogue(string scriptId)
	{
		if (Dialogue.IsActive || string.IsNullOrEmpty(scriptId))
			return;
		Dialogue.Start(scriptId, OnDialogueCommand);
	}

	private void OnDialogueCommand(string name, string[] args)
	{
		string arg = args.Length > 0 ? args[0] : "";
		switch (name)
		{
			case "cutscene":
				PlayCutscene(arg, () => Dialogue.Resume());
				break;
			case "travel":
				GoToLocation(arg);
				break;
			case "map":
				GoToMap();
				break;
		}
	}

	// ---------------------------------------------------------------- cutscenes

	/// <summary>Plays a 3D cutscene full screen, then returns to the current scene.</summary>
	public async void PlayCutscene(string id, Action onDone)
	{
		if (!Cutscenes.TryGetValue(id, out var path) || !ResourceLoader.Exists(path))
		{
			GD.PushError($"Unknown cutscene '{id}'");
			onDone?.Invoke();
			return;
		}

		_returnScene = GetTree().CurrentScene?.SceneFilePath;
		await FadeTo(1f, 0.6f);

		var scene = GD.Load<PackedScene>(path).Instantiate();
		// Cutscenes that can loop for previewing play once inside the game.
		if (scene.Get("Loop").VariantType == Variant.Type.Bool)
			scene.Set("Loop", false);
		ReplaceCurrentScene(scene);
		_cutscene = scene;

		bool done = false;
		void Finish()
		{
			if (done) return;
			done = true;
			_cutscene = null;
			EndCutscene(onDone);
		}
		// C# [Signal] FinishedEventHandler -> "Finished"; GDScript cutscenes may use "finished".
		string signal = scene.HasSignal("Finished") ? "Finished" : scene.HasSignal("finished") ? "finished" : null;
		if (signal != null)
			scene.Connect(signal, Callable.From(Finish));
		else
			GetTree().CreateTimer(5.0).Timeout += Finish;
		_skipCutscene = Finish;

		await FadeTo(0f, 0.6f);
	}

	private Action _skipCutscene;

	private async void EndCutscene(Action onDone)
	{
		_skipCutscene = null;
		await FadeTo(1f, 0.6f);
		if (!string.IsNullOrEmpty(_returnScene))
			ReplaceCurrentScene(GD.Load<PackedScene>(_returnScene).Instantiate());
		await FadeTo(0f, 0.6f);
		onDone?.Invoke();
	}

	// ---------------------------------------------------------------- scene switching

	private async void SwitchTo(string path)
	{
		if (_switching)
			return;
		_switching = true;
		await FadeTo(1f, 0.35f);
		ReplaceCurrentScene(GD.Load<PackedScene>(path).Instantiate());
		await FadeTo(0f, 0.35f);
		_switching = false;
	}

	private void ReplaceCurrentScene(Node scene)
	{
		var tree = GetTree();
		var old = tree.CurrentScene;
		tree.Root.AddChild(scene);
		tree.CurrentScene = scene;
		Hud?.SetGameScreen(scene is LocationScreen or MapScreen);
		old?.QueueFree();
	}

	private async System.Threading.Tasks.Task FadeTo(float alpha, float seconds)
	{
		_fade.MouseFilter = alpha > 0.5f ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
		var tween = CreateTween();
		tween.TweenProperty(_fade, "modulate:a", alpha, seconds);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	// ---------------------------------------------------------------- input

	public override void _UnhandledInput(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel") && _skipCutscene != null)
		{
			_skipCutscene();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!OS.IsDebugBuild() || e is not InputEventKey { Pressed: true, Echo: false } key)
			return;
		if (key.Keycode == Key.F9)
		{
			var state = GameState.Instance;
			state.SetFlag(GameState.TavernKeeperDead, !state.IsAfter);
			GD.Print($"[debug] act: {(state.IsAfter ? "after" : "before")} the Tavern Keeper's death");
			Dialogue.Stop();
			string current = GetTree().CurrentScene?.SceneFilePath;
			if (!string.IsNullOrEmpty(current))
				SwitchTo(current);
		}
		else if (key.Keycode == Key.F10)
		{
			Dialogue.Stop();
			GoToMap();
		}
	}

	// ---------------------------------------------------------------- theme

	private static Theme BuildTheme()
	{
		var theme = new Theme();
		var font = GD.Load<Font>("res://assets/fonts/EBGaramond.ttf");
		if (font != null)
			theme.DefaultFont = font;
		theme.DefaultFontSize = 32;

		var ink = new Color(0.95f, 0.9f, 0.8f);
		var gold = new Color(0.93f, 0.78f, 0.48f);
		StyleBoxFlat Box(float alpha, Color border) => new()
		{
			BgColor = new Color(0.08f, 0.06f, 0.05f, alpha),
			BorderColor = border,
			BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 8, ContentMarginBottom = 8,
		};
		theme.SetStylebox("normal", "Button", Box(0.78f, new Color(0.55f, 0.44f, 0.27f, 0.8f)));
		theme.SetStylebox("hover", "Button", Box(0.9f, gold));
		theme.SetStylebox("pressed", "Button", Box(0.95f, gold));
		theme.SetStylebox("focus", "Button", Box(0.9f, gold));
		theme.SetStylebox("disabled", "Button", Box(0.5f, new Color(0.3f, 0.26f, 0.2f, 0.6f)));
		theme.SetColor("font_color", "Button", ink);
		theme.SetColor("font_hover_color", "Button", gold);
		theme.SetColor("font_focus_color", "Button", gold);
		theme.SetColor("font_pressed_color", "Button", gold);
		theme.SetColor("font_disabled_color", "Button", new Color(0.5f, 0.46f, 0.4f));
		theme.SetColor("font_color", "Label", ink);
		theme.SetColor("font_shadow_color", "Label", new Color(0, 0, 0, 0.8f));
		theme.SetConstant("shadow_offset_x", "Label", 2);
		theme.SetConstant("shadow_offset_y", "Label", 2);
		return theme;
	}
}
