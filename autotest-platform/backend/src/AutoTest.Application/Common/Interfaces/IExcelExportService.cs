namespace AutoTest.Application.Common.Interfaces;

public interface IExcelExportService
{
    Task<Stream> ExportUsersReportAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, string? subscriptionStatus, CancellationToken ct = default);
    Task<Stream> ExportRevenueReportAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken ct = default);
    Task<Stream> ExportExamStatsReportAsync(DateTimeOffset? dateFrom, DateTimeOffset? dateTo, CancellationToken ct = default);
    Task<Stream> ExportQuestionsReportAsync(CancellationToken ct = default);
    byte[] GenerateQuestionImportTemplate();
}
