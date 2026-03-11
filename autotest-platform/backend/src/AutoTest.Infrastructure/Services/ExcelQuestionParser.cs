using AutoTest.Application.Common.Interfaces;
using ClosedXML.Excel;

namespace AutoTest.Infrastructure.Services;

// Excel columns (1-based): 1=TicketNumber, 2=Order, 3=CategorySlug, 4=Difficulty, 5=LicenseCategory,
// 6=TextUz, 7=TextUzLatin, 8=TextRu, 9=ExplanationUz, 10=ExplanationUzLatin, 11=ExplanationRu,
// 12=QuestionImage, 13=CorrectAnswer, 14=IsActive,
// 15=Opt1TextUz, 16=Opt1TextUzLatin, 17=Opt1TextRu, 18=Opt1Image,
// 19=Opt2TextUz, 20=Opt2TextUzLatin, 21=Opt2TextRu, 22=Opt2Image,
// 23=Opt3TextUz, 24=Opt3TextUzLatin, 25=Opt3TextRu, 26=Opt3Image,
// 27=Opt4TextUz, 28=Opt4TextUzLatin, 29=Opt4TextRu, 30=Opt4Image
public class ExcelQuestionParser : IQuestionImportService
{
    public Task<QuestionImportResult> ParseExcelAsync(Stream excelStream, CancellationToken ct = default)
    {
        var questions = new List<ImportQuestionDto>();
        var errors = new List<ImportRowError>();

        using var wb = new XLWorkbook(excelStream);
        var ws = wb.Worksheets.First();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var ticket = ParseInt(ws, row, 1, "TicketNumber", errors);
                var order = ParseInt(ws, row, 2, "Order", errors);
                var categorySlug = ParseString(ws, row, 3, "CategorySlug", errors);
                var difficulty = ParseInt(ws, row, 4, "Difficulty", errors);
                var licenseCategory = ParseString(ws, row, 5, "LicenseCategory", errors);
                var textUz = ParseString(ws, row, 6, "TextUz", errors);
                var textUzLatin = ws.Cell(row, 7).GetValue<string>() ?? "";
                var textRu = ParseString(ws, row, 8, "TextRu", errors);
                var explUz = ws.Cell(row, 9).GetValue<string>() ?? "";
                var explUzLatin = ws.Cell(row, 10).GetValue<string>() ?? "";
                var explRu = ws.Cell(row, 11).GetValue<string>() ?? "";
                var qImage = ws.Cell(row, 12).GetValue<string>();
                var correctAnswer = ParseString(ws, row, 13, "CorrectAnswer", errors);
                var isActive = ws.Cell(row, 14).GetValue<string>()?.Trim().ToLower() is "true" or "1" or "да" or "yes";

                if (errors.Any(e => e.Row == row))
                    continue;

                var options = new List<ImportAnswerOptionDto>();
                for (var opt = 0; opt < 4; opt++)
                {
                    var col = 15 + opt * 4;
                    var optTextUz = ws.Cell(row, col).GetValue<string>() ?? "";
                    var optTextUzLatin = ws.Cell(row, col + 1).GetValue<string>() ?? "";
                    var optTextRu = ws.Cell(row, col + 2).GetValue<string>() ?? "";
                    var optImage = ws.Cell(row, col + 3).GetValue<string>();

                    if (!string.IsNullOrWhiteSpace(optTextUz) || !string.IsNullOrWhiteSpace(optTextRu))
                        options.Add(new ImportAnswerOptionDto(optTextUz, optTextUzLatin, optTextRu, optImage));
                }

                if (options.Count < 2)
                {
                    errors.Add(new ImportRowError(row, "Options", "At least 2 answer options required"));
                    continue;
                }

                questions.Add(new ImportQuestionDto(
                    ticket, order, categorySlug!, difficulty, licenseCategory!,
                    textUz!, textUzLatin, textRu!, explUz, explUzLatin, explRu,
                    string.IsNullOrWhiteSpace(qImage) ? null : qImage,
                    options, correctAnswer!, isActive));
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError(row, "General", ex.Message));
            }
        }

        return Task.FromResult(new QuestionImportResult(questions, errors));
    }

    private static int ParseInt(IXLWorksheet ws, int row, int col, string column, List<ImportRowError> errors)
    {
        var val = ws.Cell(row, col).GetValue<string>();
        if (int.TryParse(val, out var result))
            return result;
        errors.Add(new ImportRowError(row, column, $"Invalid integer: '{val}'"));
        return 0;
    }

    private static string? ParseString(IXLWorksheet ws, int row, int col, string column, List<ImportRowError> errors)
    {
        var val = ws.Cell(row, col).GetValue<string>()?.Trim();
        if (!string.IsNullOrWhiteSpace(val))
            return val;
        errors.Add(new ImportRowError(row, column, "Required field is empty"));
        return null;
    }
}
