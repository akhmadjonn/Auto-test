namespace AutoTest.Application.Common.Interfaces;

public interface IQuestionImportService
{
    Task<QuestionImportResult> ParseExcelAsync(Stream excelStream, CancellationToken ct = default);
}

public record QuestionImportResult(List<ImportQuestionDto> Questions, List<ImportRowError> Errors);

public record ImportQuestionDto(
    int TicketNumber,
    int QuestionOrder,
    string CategorySlug,
    int Difficulty,
    string LicenseCategory,
    string TextUz,
    string TextUzLatin,
    string TextRu,
    string ExplanationUz,
    string ExplanationUzLatin,
    string ExplanationRu,
    string? QuestionImageFileName,
    List<ImportAnswerOptionDto> Options,
    string CorrectAnswer,
    bool IsActive);

public record ImportAnswerOptionDto(string TextUz, string TextUzLatin, string TextRu, string? ImageFileName);

public record ImportRowError(int Row, string Column, string Error);
