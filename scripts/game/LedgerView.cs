using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The harbour ledger, opened from the office (the Ledger hotspot has <c>Opens = ledger</c>):
/// a big account book on the table, two pages at a time, turned with the arrows below it.
/// Pages come from dialogs/&lt;language&gt;/ledger.txt (see <see cref="Load"/>); a row that
/// points at a script can be clicked and runs it, over the open book.
/// </summary>
public partial class LedgerView : Control
{
	// Writing areas on the pages of assets/art/ledger/ledger_book.png, in the 1920x1080 frame.
	private static readonly Rect2 LeftPage = new(292, 92, 636, 890);
	private static readonly Rect2 RightPage = new(1018, 92, 628, 890);
	private static readonly Rect2 Book = new(190, 40, 1560, 1010);
	private static readonly Color Ink = new(0.16f, 0.09f, 0.04f, 0.94f);
	private static readonly Color Chalk = new(0.95f, 0.88f, 0.72f);

	private sealed record Row(string[] Cells, string Script, string Condition);
	private sealed record Page(string Title, List<Row> Rows);

	private Control _frame;
	private RichTextLabel _left, _right;
	private Label _leftNumber, _rightNumber, _closeHint;
	private Button _prev, _next;
	private Font _hand;
	private int _spread;
	private List<Page> _pages = new();

	public override void _Ready()
	{
		Theme = Game.UiTheme;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_hand = GD.Load<Font>("res://assets/fonts/MarckScript-Regular.ttf");

		var book = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/art/ledger/ledger_book.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		book.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(book);

		_frame = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(1920, 1080) };
		AddChild(_frame);
		Resized += FitFrame;

		_left = PageLabel(LeftPage, -0.4f);
		_right = PageLabel(RightPage, 0.3f);
		_leftNumber = Number(new Vector2(LeftPage.Position.X, 990), HorizontalAlignment.Left);
		_rightNumber = Number(new Vector2(RightPage.End.X - 120, 990), HorizontalAlignment.Right);

		_prev = Arrow("‹", new Vector2(Book.Position.X - 150, 470), -1);
		_next = Arrow("›", new Vector2(Book.End.X + 30, 470), 1);

		_closeHint = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			Position = new Vector2(1320, 1034),
			Size = new Vector2(560, 46),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_closeHint.AddThemeFontSizeOverride("font_size", 28);
		_closeHint.AddThemeColorOverride("font_color", new Color(0.92f, 0.84f, 0.68f, 0.75f));
		_frame.AddChild(_closeHint);

		// A click on the table around the book puts it away; on the book it does nothing.
		GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
				&& !Book.HasPoint(ToFrame(click.Position)))
				Close();
		};
		GameState.Instance.Changed += () => { if (Visible) Fill(); };
		_candle = Night.Candlelight(new Vector2(0.5f, 0.5f));
		AddChild(_candle);
		FitFrame();
	}

	private ColorRect _candle;

	public void Open()
	{
		_candle.Visible = Night.IsNight;
		_spread = 0;
		Fill();
		Show();
	}

	public void Close() => Hide();

	/// <summary>Turns a spread back (-1) or forward (+1); also the arrow keys.</summary>
	public void Turn(int direction)
	{
		int last = Mathf.Max(0, (_pages.Count - 1) / 2);
		int spread = Mathf.Clamp(_spread + direction, 0, last);
		if (spread == _spread)
			return;
		_spread = spread;
		Fill();
	}

	public void Fill()
	{
		_pages = Load(GameState.Instance.Language);
		int last = Mathf.Max(0, (_pages.Count - 1) / 2);
		_spread = Mathf.Clamp(_spread, 0, last);
		_left.Text = Render(PageAt(_spread * 2));
		_right.Text = Render(PageAt(_spread * 2 + 1));
		_leftNumber.Text = PageAt(_spread * 2) != null ? $"{_spread * 2 + 1}" : "";
		_rightNumber.Text = PageAt(_spread * 2 + 1) != null ? $"{_spread * 2 + 2}" : "";
		_prev.Visible = _spread > 0;
		_next.Visible = _spread < last;
		_closeHint.Text = Tr("ledger_close_hint");
	}

	private Page PageAt(int i) => i < _pages.Count ? _pages[i] : null;

	private string Render(Page page)
	{
		if (page == null)
			return "";
		var state = GameState.Instance;
		var rows = page.Rows.Where(r => state.Check(r.Condition)).ToList();
		var text = new StringBuilder();
		text.Append("[center][font_size=46]").Append(page.Title).Append("[/font_size][/center]\n");
		if (rows.Count == 0)
			return text.ToString();

		// Column widths: room for the longest entry, but never less than the longest single
		// word, so a word is never broken across lines.
		int columns = rows.Max(r => r.Cells.Length);
		var widths = new int[columns];
		foreach (var row in rows)
			for (int c = 0; c < row.Cells.Length; c++)
			{
				string cell = row.Cells[c];
				int longestWord = cell.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Select(w => w.Length).DefaultIfEmpty(0).Max();
				int want = Mathf.Max((int)(Mathf.Min(cell.Length, 24) * 0.8f), (int)(longestWord * 1.4f));
				widths[c] = Mathf.Max(widths[c], Mathf.Max(want, 4));
			}

		text.Append($"[table={columns}]");
		for (int r = 0; r < rows.Count; r++)
		{
			var row = rows[r];
			for (int c = 0; c < columns; c++)
			{
				string cell = c < row.Cells.Length ? row.Cells[c] : "";
				text.Append($"[cell expand={widths[c]} padding=4,6,10,6]");
				if (r == 0)
					text.Append($"[color=#{new Color(0.42f, 0.16f, 0.1f).ToHtml(false)}]").Append(cell).Append("[/color]");
				else if (row.Script.Length > 0)
					text.Append($"[url={row.Script}]").Append(cell).Append("[/url]");
				else
					text.Append(cell);
				text.Append("[/cell]");
			}
		}
		text.Append("[/table]");
		return text.ToString();
	}

	/// <summary>
	/// Reads the ledger pages: <c>== Title</c> starts a page, then one row per line with
	/// cells split by <c>|</c> (the first row is the column headings). A row ending in
	/// <c>-&gt; script/id</c> can be clicked; <c>@if condition</c> at the very end shows the
	/// row only while it holds. Falls back to English.
	/// </summary>
	private static List<Page> Load(string language)
	{
		string path = $"res://dialogs/{language}/ledger.txt";
		if (!FileAccess.FileExists(path))
			path = "res://dialogs/en/ledger.txt";
		var pages = new List<Page>();
		if (!FileAccess.FileExists(path))
			return pages;

		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		foreach (var raw in file.GetAsText().TrimStart('﻿').Split('\n'))
		{
			string line = raw.Trim();
			if (line.Length == 0 || line.StartsWith('#'))
				continue;
			if (line.StartsWith("=="))
			{
				pages.Add(new Page(line[2..].Trim(), new List<Row>()));
				continue;
			}
			if (pages.Count == 0)
				continue;

			string condition = "";
			int at = line.LastIndexOf("@if ", System.StringComparison.Ordinal);
			if (at >= 0)
			{
				condition = line[(at + 4)..].Trim();
				line = line[..at].Trim();
			}
			string script = "";
			int arrow = line.LastIndexOf("->", System.StringComparison.Ordinal);
			if (arrow >= 0)
			{
				script = line[(arrow + 2)..].Trim();
				line = line[..arrow].Trim();
			}
			var cells = line.Split('|').Select(c => c.Trim()).ToArray();
			pages[^1].Rows.Add(new Row(cells, script, condition));
		}
		return pages;
	}

	private RichTextLabel PageLabel(Rect2 area, float tilt)
	{
		var page = new RichTextLabel
		{
			BbcodeEnabled = true,
			ScrollActive = false,
			Position = area.Position,
			Size = area.Size,
			Rotation = Mathf.DegToRad(tilt),
			MouseFilter = MouseFilterEnum.Pass,
			MetaUnderlined = true,
		};
		page.AddThemeFontOverride("normal_font", _hand);
		page.AddThemeFontSizeOverride("normal_font_size", 28);
		page.AddThemeColorOverride("default_color", Ink);
		page.AddThemeConstantOverride("line_separation", -4);
		page.MetaClicked += meta =>
		{
			string script = meta.AsString();
			if (!string.IsNullOrEmpty(script))
				Game.Instance.RunDialogue(script);
		};
		page.MetaHoverStarted += _ => page.MouseDefaultCursorShape = CursorShape.PointingHand;
		page.MetaHoverEnded += _ => page.MouseDefaultCursorShape = CursorShape.Arrow;
		_frame.AddChild(page);
		return page;
	}

	private Label Number(Vector2 at, HorizontalAlignment align)
	{
		var label = new Label { Position = at, Size = new Vector2(120, 40), HorizontalAlignment = align, MouseFilter = MouseFilterEnum.Ignore };
		label.AddThemeFontOverride("font", _hand);
		label.AddThemeFontSizeOverride("font_size", 28);
		label.AddThemeColorOverride("font_color", Ink);
		label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
		_frame.AddChild(label);
		return label;
	}

	private Button Arrow(string glyph, Vector2 at, int direction)
	{
		var button = new Button
		{
			Text = glyph,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			Position = at,
			Size = new Vector2(120, 140),
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", _hand);
		button.AddThemeFontSizeOverride("font_size", 120);
		button.AddThemeColorOverride("font_color", Chalk);
		button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.85f, 0.5f));
		button.Pressed += () => Turn(direction);
		_frame.AddChild(button);
		return button;
	}

	private Vector2 ToFrame(Vector2 local) => (local - _frame.Position) / _frame.Scale;

	private void FitFrame()
	{
		Vector2 screen = Size == Vector2.Zero ? new Vector2(1920, 1080) : Size;
		float scale = Mathf.Max(screen.X / 1920f, screen.Y / 1080f);
		_frame.Scale = Vector2.One * scale;
		_frame.Position = (screen - new Vector2(1920, 1080) * scale) / 2f;
	}
}
