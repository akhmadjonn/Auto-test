using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Questions;

public record GetTicketsListQuery(LicenseCategory? LicenseCategory = null) : IRequest<ApiResponse<List<TicketSummaryDto>>>;

public record TicketSummaryDto(int TicketNumber, int QuestionCount);

public class GetTicketsListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTicketsListQuery, ApiResponse<List<TicketSummaryDto>>>
{
    public async Task<ApiResponse<List<TicketSummaryDto>>> Handle(GetTicketsListQuery request, CancellationToken ct)
    {
        var query = db.Questions.AsNoTracking().Where(q => q.IsActive);

        if (request.LicenseCategory.HasValue && request.LicenseCategory != Domain.Common.Enums.LicenseCategory.Both)
            query = query.Where(q => q.LicenseCategory == request.LicenseCategory.Value
                || q.LicenseCategory == Domain.Common.Enums.LicenseCategory.Both);

        var tickets = await query
            .GroupBy(q => q.TicketNumber)
            .Select(g => new TicketSummaryDto(g.Key, g.Count()))
            .OrderBy(t => t.TicketNumber)
            .ToListAsync(ct);

        return ApiResponse<List<TicketSummaryDto>>.Ok(tickets);
    }
}
