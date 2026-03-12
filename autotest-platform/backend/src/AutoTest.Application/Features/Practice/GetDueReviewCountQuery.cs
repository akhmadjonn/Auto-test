using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Practice;

public record GetDueReviewCountQuery(Guid? CategoryId = null) : IRequest<ApiResponse<DueReviewCountDto>>;

public record DueReviewCountDto(int DueCount);

public class GetDueReviewCountQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime) : IRequestHandler<GetDueReviewCountQuery, ApiResponse<DueReviewCountDto>>
{
    public async Task<ApiResponse<DueReviewCountDto>> Handle(GetDueReviewCountQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<DueReviewCountDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;
        var now = dateTime.UtcNow;

        var query = db.UserQuestionStates
            .Where(s => s.UserId == userId && s.NextReviewDate <= now);

        if (request.CategoryId.HasValue)
            query = query.Where(s => s.Question.CategoryId == request.CategoryId.Value);

        var count = await query.CountAsync(ct);

        return ApiResponse<DueReviewCountDto>.Ok(new DueReviewCountDto(count));
    }
}
