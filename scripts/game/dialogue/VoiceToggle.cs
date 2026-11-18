using Godot;

/// <summary>
/// The small speaker in the corner of the dialogue box: switches the voice-over on and off
/// (<see cref="Settings.VoiceOn"/>). Drawn in code: a speaker with sound waves when on, with a
/// cross when off.
/// </summary>
public partial class VoiceToggle : Button
{
	private static readonly Color Gold = new(0.93f, 0.78f, 0.48f);
	private static readonly Color Dim = new(0.62f, 0.5f, 0.3f);

	public override void _Ready()
	{
		Flat = true;
		FocusMode = FocusModeEnum.None;   // so Space / Enter keep advancing the dialogue
		MouseDefaultCursorShape = CursorShape.PointingHand;
		Pressed += () => Settings.SetVoiceOn(!Settings.VoiceOn);
		Settings.Changed += OnSettingsChanged;
		MouseEntered += QueueRedraw;
		MouseExited += QueueRedraw;
		TooltipText = " ";   // non-empty, so _GetTooltip is asked
	}

	public override void _ExitTree() => Settings.Changed -= OnSettingsChanged;

	private void OnSettingsChanged() => QueueRedraw();

	// Worked out when shown, so it follows both the switch and the language.
	public override string _GetTooltip(Vector2 atPosition) => Tr(Settings.VoiceOn ? "voice_on_tip" : "voice_off_tip");

	public override void _Draw()
	{
		var color = IsHovered() ? Gold : Dim;
		float s = Mathf.Min(Size.X, Size.Y);
		var o = (Size - new Vector2(s, s)) / 2f;
		Vector2 P(float x, float y) => o + new Vector2(x, y) * s;

		// The speaker: a small box and its cone.
		DrawColoredPolygon(new[]
		{
			P(0.14f, 0.38f), P(0.3f, 0.38f), P(0.5f, 0.2f), P(0.5f, 0.8f), P(0.3f, 0.62f), P(0.14f, 0.62f),
		}, color);

		float w = s * 0.055f;
		if (Settings.VoiceOn)
		{
			// Two waves.
			DrawArc(P(0.5f, 0.5f), s * 0.16f, -0.75f, 0.75f, 12, color, w, true);
			DrawArc(P(0.5f, 0.5f), s * 0.3f, -0.8f, 0.8f, 16, color, w, true);
		}
		else
		{
			DrawLine(P(0.62f, 0.37f), P(0.86f, 0.63f), color, w, true);
			DrawLine(P(0.62f, 0.63f), P(0.86f, 0.37f), color, w, true);
		}
	}
}
