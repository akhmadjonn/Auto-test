using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;

namespace AutoTest.Application.Features.Admin;

public record ExportUsersReportCommand(
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    string? SubscriptionStatus = null) : IRequest<ApiResponse<Stream>>;

public class ExportUsersReportCommandHandler(
    IExcelExportService excelService) : IRequestHandler<ExportUsersReportCommand, ApiResponse<Stream>>
{
    public async Task<ApiResponse<Stream>> Handle(ExportUsersReportCommand request, CancellationToken ct)
    {
        var stream = await excelService.ExportUsersReportAsync(request.DateFrom, request.DateTo, request.SubscriptionStatus, ct);
        return ApiResponse<Stream>.Ok(stream);
    }
}

public record ExportExamStatsReportCommand(
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null) : IRequest<ApiResponse<Stream>>;

public class ExportExamStatsReportCommandHandler(
    IExcelExportService excelService) : IRequestHandler<ExportExamStatsReportCommand, ApiResponse<Stream>>
{
    public async Task<ApiResponse<Stream>> Handle(ExportExamStatsReportCommand request, CancellationToken ct)
    {
        var stream = await excelService.ExportExamStatsReportAsync(request.DateFrom, request.DateTo, ct);
        return ApiResponse<Stream>.Ok(stream);
    }
}

public record ExportQuestionsReportCommand : IRequest<ApiResponse<Stream>>;

public class ExportQuestionsReportCommandHandler(
    IExcelExportService excelService) : IRequestHandler<ExportQuestionsReportCommand, ApiResponse<Stream>>
{
    public async Task<ApiResponse<Stream>> Handle(ExportQuestionsReportCommand request, CancellationToken ct)
    {
        var stream = await excelService.ExportQuestionsReportAsync(ct);
        return ApiResponse<Stream>.Ok(stream);
    }
}
