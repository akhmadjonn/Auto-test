using AutoTest.Application.Common.Interfaces;
using ClosedXML.Excel;

namespace AutoTest.Infrastructure.Services;

public class ExcelExportService : IExcelExportService
{
    private static readonly string[] QuestionHeaders =
    [
        "TicketNumber", "Order", "CategorySlug", "Difficulty(1-3)", "LicenseCategory(AB/CD/Both)",
        "TextUz(Cyrillic)", "TextUzLatin", "TextRu",
        "ExplanationUz", "ExplanationUzLatin", "ExplanationRu",
        "QuestionImageFile", "CorrectAnswerText", "IsActive(true/false)",
        "Option1TextUz", "Option1TextUzLatin", "Option1TextRu", "Option1ImageFile",
        "Option2TextUz", "Option2TextUzLatin", "Option2TextRu", "Option2ImageFile",
        "Option3TextUz", "Option3TextUzLatin", "Option3TextRu", "Option3ImageFile",
        "Option4TextUz", "Option4TextUzLatin", "Option4TextRu", "Option4ImageFile"
    ];

    public byte[] GenerateQuestionImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Questions");

        for (var i = 0; i < QuestionHeaders.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = QuestionHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        // Example row
        ws.Cell(2, 1).Value = 1;
        ws.Cell(2, 2).Value = 1;
        ws.Cell(2, 3).Value = "road-signs";
        ws.Cell(2, 4).Value = 1;
        ws.Cell(2, 5).Value = "AB";
        ws.Cell(2, 6).Value = "Қайси белги";
        ws.Cell(2, 7).Value = "Qaysi belgi";
        ws.Cell(2, 8).Value = "Какой знак";
        ws.Cell(2, 9).Value = "Tayanch belgi";
        ws.Cell(2, 10).Value = "Asosiy belgi";
        ws.Cell(2, 11).Value = "Основной знак";
        ws.Cell(2, 12).Value = "";
        ws.Cell(2, 13).Value = "To'g'ri javob";
        ws.Cell(2, 14).Value = "true";
        ws.Cell(2, 15).Value = "To'g'ri javob";
        ws.Cell(2, 16).Value = "To'g'ri javob";
        ws.Cell(2, 17).Value = "Правильный ответ";
        ws.Cell(2, 18).Value = "";
        ws.Cell(2, 19).Value = "Noto'g'ri 1";
        ws.Cell(2, 20).Value = "Noto'g'ri 1";
        ws.Cell(2, 21).Value = "Неправильный 1";
        ws.Cell(2, 22).Value = "";
        ws.Cell(2, 23).Value = "Noto'g'ri 2";
        ws.Cell(2, 24).Value = "Noto'g'ri 2";
        ws.Cell(2, 25).Value = "Неправильный 2";
        ws.Cell(2, 26).Value = "";

        // Add dropdown validation via ranges
        ws.Range(2, 4, 1001, 4).CreateDataValidation().List("\"1,2,3\"");
        ws.Range(2, 5, 1001, 5).CreateDataValidation().List("\"AB,CD,Both\"");
        ws.Range(2, 14, 1001, 14).CreateDataValidation().List("\"true,false\"");

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public Task<Stream> ExportUsersReportAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, string? subscriptionStatus, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<Stream> ExportRevenueReportAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<Stream> ExportExamStatsReportAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<Stream> ExportQuestionsReportAsync(CancellationToken ct = default) =>
        Task.FromResult<Stream>(new MemoryStream());
}
