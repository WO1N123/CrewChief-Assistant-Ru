using System.Text.RegularExpressions;

namespace CrewChiefRUAssistant.Responses;

public static class RussianSpeakerMorphology
{
    private sealed record Form(
        Regex MalePattern,
        Regex FemalePattern,
        string Male,
        string Female);

    private static readonly IReadOnlyList<Form> Forms =
    [
        Create("я не понял", "я не поняла"),
        Create("я понял", "я поняла"),
        Create("я готов", "я готова"),
        Create("я уверен", "я уверена"),
        Create("я не уверен", "я не уверена"),
        Create("я проверил", "я проверила"),
        Create("я рассчитал", "я рассчитала"),
        Create("я услышал", "я услышала"),
        Create("я заметил", "я заметила")
    ];

    public static AssistantResponse Apply(
        AssistantResponse response,
        string? voiceId)
    {
        var text =
            Apply(
                response.Text,
                voiceId);

        return text == response.Text
            ? response
            : response with
            {
                Text = text
            };
    }

    public static string Apply(
        string text,
        string? voiceId)
    {
        var female =
            AppConfig.NormalizeVoiceId(
                voiceId) == "xenia";

        foreach (var form in Forms)
        {
            var pattern =
                female
                    ? form.MalePattern
                    : form.FemalePattern;

            var replacement =
                female
                    ? form.Female
                    : form.Male;

            text =
                pattern.Replace(
                    text,
                    match => PreserveInitialCase(
                        match.Value,
                        replacement));
        }

        return text;
    }

    private static Form Create(
        string male,
        string female) =>
        new(
            Word(male),
            Word(female),
            male,
            female);

    private static Regex Word(
        string text) =>
        new(
            $@"\b{Regex.Escape(text).Replace(@"\ ", @"\s+")}\b",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled);

    private static string PreserveInitialCase(
        string source,
        string replacement)
    {
        if (string.IsNullOrEmpty(source) ||
            string.IsNullOrEmpty(replacement) ||
            !char.IsUpper(source[0]))
        {
            return replacement;
        }

        return char.ToUpperInvariant(
                   replacement[0]) +
               replacement[1..];
    }
}
