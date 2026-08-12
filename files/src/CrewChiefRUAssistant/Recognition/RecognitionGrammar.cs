using System.Text.Encodings.Web;
using System.Text.Json;
using CrewChiefRUAssistant.Intent;

namespace CrewChiefRUAssistant.Recognition;

public static class RecognitionGrammar
{
    private static readonly Lazy<string> Json =
        new(CreateJson);

    public static string CommandJson => Json.Value;

    private static string CreateJson()
    {
        var phrases = IntentPhraseCatalog.AllPhrases
            .Append("[unk]")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return JsonSerializer.Serialize(
            phrases,
            new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
    }
}
