/// <summary>
/// What a dialogue script can change on screen. Implemented by <see cref="LocationScreen"/>.
/// </summary>
public interface IStage
{
	/// <summary><c>@bg id</c>: shows assets/art/backgrounds/&lt;id&gt;.png (or its _after variant).</summary>
	void SetBackground(string id);

	/// <summary><c>@show id [expression] [left|center|right|0..1]</c>.</summary>
	void ShowCharacter(string id, string expression, string position);

	/// <summary>"Name [expression]: …" — swaps a shown character's sprite for one line; an empty
	/// expression puts back the one set by <c>@show</c>.</summary>
	void SetLineExpression(string id, string expression);

	/// <summary><c>@hide id</c> or <c>@hide all</c>.</summary>
	void HideCharacter(string id);

	/// <summary>Dialogue opened / closed: hide clickable things, put them back.</summary>
	void OnDialogueStarted();
	void OnDialogueEnded();
}
