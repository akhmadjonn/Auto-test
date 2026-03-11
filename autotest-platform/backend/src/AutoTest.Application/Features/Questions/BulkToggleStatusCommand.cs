using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Questions;

public record BulkToggleStatusCommand(List<Guid> QuestionIds, bool IsActive) : IRequest<ApiResponse<int>>;

public class BulkToggleStatusCommandValidator : AbstractValidator<BulkToggleStatusCommand>
{
    public BulkToggleStatusCommandValidator()
    {
        RuleFor(x => x.QuestionIds).NotEmpty().Must(ids => ids.Count <= 1000).WithMessage("Max 1000 at once");
    }
}

public class BulkToggleStatusCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime) : IRequestHandler<BulkToggleStatusCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(BulkToggleStatusCommand request, CancellationToken ct)
    {
        var questions = await db.Questions
            .Where(q => request.QuestionIds.Contains(q.Id))
            .ToListAsync(ct);

        var now = dateTime.UtcNow;
        foreach (var q in questions)
        {
            q.IsActive = request.IsActive;
            q.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return ApiResponse<int>.Ok(questions.Count);
    }
}
