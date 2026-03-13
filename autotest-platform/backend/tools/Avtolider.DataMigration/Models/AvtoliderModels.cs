using System.Text.Json.Serialization;

namespace Avtolider.DataMigration.Models;

public record AvtoliderTheme(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name_uz")] string NameUz,
    [property: JsonPropertyName("name_ru")] string NameRu);

public record AvtoliderQuestion(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("question_uz")] string QuestionUz,
    [property: JsonPropertyName("question_ru")] string QuestionRu,
    [property: JsonPropertyName("image_url")] string? ImageUrl,
    [property: JsonPropertyName("theme_id")] int ThemeId,
    [property: JsonPropertyName("is_active")] bool IsActive);

public record AvtoliderOption(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("quiz_id")] int QuizId,
    [property: JsonPropertyName("text_uz")] string TextUz,
    [property: JsonPropertyName("text_ru")] string TextRu,
    [property: JsonPropertyName("is_correct")] bool IsCorrect);
