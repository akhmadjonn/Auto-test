using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;

namespace AutoTest.Application.Features.Questions;

public record ExportExcelTemplateQuery : IRequest<ApiResponse<byte[]>>;

public class ExportExcelTemplateQueryHandler(IExcelExportService excelService)
    : IRequestHandler<ExportExcelTemplateQuery, ApiResponse<byte[]>>
{
    public Task<ApiResponse<byte[]>> Handle(ExportExcelTemplateQuery request, CancellationToken ct)
    {
        var bytes = excelService.GenerateQuestionImportTemplate();
        return Task.FromResult(ApiResponse<byte[]>.Ok(bytes));
    }
}
