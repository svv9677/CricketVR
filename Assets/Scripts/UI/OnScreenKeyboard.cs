/// <summary>
/// The rules of the in-world name keyboard (CreatePlayerScreen), kept apart from the screen so they
/// can be tested without a scene. There is no Shift key: the keyboard capitalises the first letter
/// of every word for you ("asha patel-rao" types as "Asha Patel-Rao", and a single letter before an
/// apostrophe starts the word again, so "o'neill" is "O'Neill"). Names are letters, spaces, hyphens
/// and apostrophes only - all inside Latin-1, which is all the TMP default font can draw - with no
/// separator first or two in a row, and at most MaxLength characters so a name fits the scoreboard.
/// </summary>
public static class OnScreenKeyboard
{
    public const int MaxLength = 16;
    /// The fewest letters a name needs before Next accepts it.
    public const int MinLetters = 2;

    /// The letter rows, top to bottom, as the builder lays them out (QWERTY).
    public static readonly string[] Rows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };

    public const string Space = " ", Hyphen = "-", Apostrophe = "'";

    /// `text` with `key` typed on the end, or `text` unchanged when the key is not allowed there.
    public static string Type(string text, string key)
    {
        text ??= "";
        if (string.IsNullOrEmpty(key) || key.Length != 1 || text.Length >= MaxLength)
            return text;
        char c = key[0];
        if (IsSeparator(c))
        {
            // Never first, never two together ("Asha  Rao", "Rao--", "-Asha").
            if (text.Length == 0 || IsSeparator(text[text.Length - 1]))
                return text;
            return text + c;
        }
        if (!IsLetter(c))
            return text;
        return text + (StartsWord(text) ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
    }

    /// `text` with its last character removed.
    public static string Backspace(string text) =>
        string.IsNullOrEmpty(text) ? "" : text.Substring(0, text.Length - 1);

    /// The name as it is kept: no separator left dangling at the end ("Asha " -> "Asha").
    public static string Finish(string text) => (text ?? "").TrimEnd(' ', '-', '\'');

    public static int Letters(string text)
    {
        int n = 0;
        if (text != null)
            foreach (char c in text)
                if (IsLetter(c)) n++;
        return n;
    }

    /// Long enough to be a name once finished.
    public static bool IsValid(string text) => Letters(Finish(text)) >= MinLetters;

    /// `name` typed key by key from scratch: what the keyboard would make of it. A name that comes
    /// back unchanged is one the keyboard could have produced (the Random names are checked so).
    public static string Retype(string name)
    {
        string text = "";
        if (name != null)
            foreach (char c in name)
                text = Type(text, c.ToString());
        return text;
    }

    private static bool IsLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    private static bool IsSeparator(char c) => c == ' ' || c == '-' || c == '\'';

    /// The next letter starts a word: at the start, after a space or hyphen, or after an
    /// apostrophe that follows a one-letter start ("O'", "D'") rather than a longer word ("Don'").
    private static bool StartsWord(string text)
    {
        if (text.Length == 0)
            return true;
        char last = text[text.Length - 1];
        if (last == ' ' || last == '-')
            return true;
        return last == '\'' && text.Length >= 2 && (text.Length == 2 || !IsLetter(text[text.Length - 3]));
    }
}
