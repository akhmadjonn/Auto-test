using System.Text.Json.Serialization;

namespace Avtolider.DataMigration.Models;

public record ApkChoice(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("answer")] bool Answer);

public record ApkMedia(
    [property: JsonPropertyName("exist")] bool Exist,
    [property: JsonPropertyName("name")] string Name);

public record ApkQuestion(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("choises")] List<ApkChoice> Choises,
    [property: JsonPropertyName("media")] ApkMedia Media,
    [property: JsonPropertyName("description")] string? Description);
