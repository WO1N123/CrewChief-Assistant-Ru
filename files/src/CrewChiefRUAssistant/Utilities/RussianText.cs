using System.Text;

namespace CrewChiefRUAssistant.Utilities;

public sealed class NormalizedRussianText
{
    public NormalizedRussianText(string text, string[] words)
    {
        Text = text;
        Words = words;
    }

    public string Text { get; }
    public string[] Words { get; }

    public bool Has(params string[] roots)
    {
        foreach (var root in roots)
        {
            if (root.Contains(' '))
            {
                if (Text.Contains(root, StringComparison.Ordinal))
                    return true;

                continue;
            }

            foreach (var word in Words)
            {
                if (word.StartsWith(root, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }
}

public static class RussianText
{
    private static readonly string[] FillerPhrases =
    [
        "скажи мне",
        "подскажи мне",
        "можешь сказать",
        "можешь подсказать",
        "у меня",
        "у нас"
    ];

    private static readonly HashSet<string> FillerWords =
        new(StringComparer.Ordinal)
        {
            "а",
            "ну",
            "шеф",
            "инженер",
            "скажи",
            "подскажи",
            "пожалуйста",
            "вообще",
            "там",
            "бы"
        };

    public static NormalizedRussianText Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new NormalizedRussianText(string.Empty, []);

        var lower = input
            .ToLowerInvariant()
            .Replace('ё', 'е');

        var builder = new StringBuilder(lower.Length);

        foreach (var character in lower)
        {
            builder.Append(
                char.IsLetterOrDigit(character) ||
                char.IsWhiteSpace(character)
                    ? character
                    : ' ');
        }

        var text = CollapseSpaces(builder.ToString());

        if (text.Length == 0)
            return new NormalizedRussianText(string.Empty, []);

        var padded = $" {text} ";

        foreach (var phrase in FillerPhrases)
        {
            padded = padded.Replace(
                $" {phrase} ",
                " ",
                StringComparison.Ordinal);
        }

        var words = CollapseSpaces(padded)
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !FillerWords.Contains(word))
            .ToArray();

        return new NormalizedRussianText(
            string.Join(' ', words),
            words);
    }

    private static string CollapseSpaces(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSpace = true;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasSpace)
                    builder.Append(' ');

                previousWasSpace = true;
                continue;
            }

            builder.Append(character);
            previousWasSpace = false;
        }

        return builder.ToString().Trim();
    }
}
