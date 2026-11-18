using Godot;

/// <summary>
/// The player's purse, kept in the <c>stuivers</c> variable (<c>$add stuivers 40</c>,
/// <c>@if stuivers &gt;= 2</c>). 20 stuivers make a gulden; in the Low Countries a pint of beer
/// cost a stuiver or so, a carpenter earned about a gulden a day.
/// </summary>
public static class Money
{
	public const string Var = "stuivers";
	public const int StuiversPerGulden = 20;

	public static int Stuivers => (int)GameState.Instance.GetVar(Var);

	/// <summary>"2 guldens, 5 stuivers" / "2 гульдена 5 стюверов"; empty purse is "".</summary>
	public static string Format(int stuivers, string language)
	{
		int g = stuivers / StuiversPerGulden, s = stuivers % StuiversPerGulden;
		string guldens = g > 0 ? $"{g} {Word(g, language, "gulden", "guldens", "гульден", "гульдена", "гульденов")}" : "";
		string small = s > 0 ? $"{s} {Word(s, language, "stuiver", "stuivers", "стювер", "стювера", "стюверов")}" : "";
		if (guldens.Length > 0 && small.Length > 0)
			return language == "ru" ? $"{guldens} {small}" : $"{guldens}, {small}";
		return guldens + small;
	}

	private static string Word(int n, string language, string one, string many, string ru1, string ru2, string ru5)
	{
		if (language != "ru")
			return n == 1 ? one : many;
		int mod100 = n % 100, mod10 = n % 10;
		if (mod100 is >= 11 and <= 14) return ru5;
		return mod10 switch { 1 => ru1, >= 2 and <= 4 => ru2, _ => ru5 };
	}
}
