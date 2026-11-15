using Godot;

/// <summary>
/// Stand-in for the funeral procession cutscene until the 3D scene exists, so scripts can
/// already use <c>@cutscene funeral</c>.
/// </summary>
public partial class FuneralPlaceholder : Control
{
	[Signal] public delegate void FinishedEventHandler();

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var bg = new ColorRect { Color = new Color(0.04f, 0.04f, 0.05f) };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);
		var label = new Label
		{
			Text = Tr("cutscene_funeral_placeholder"),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.SetAnchorsPreset(LayoutPreset.FullRect);
		label.AddThemeFontSizeOverride("font_size", 44);
		AddChild(label);
		GetTree().CreateTimer(4.0).Timeout += () => EmitSignal(SignalName.Finished);
	}
}
