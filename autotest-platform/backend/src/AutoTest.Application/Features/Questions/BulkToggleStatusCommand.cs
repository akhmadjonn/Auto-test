using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Questions;

public record BulkToggleStatusCommand(List<Guid> QuestionIds, QuestionStatus Status) : IRequest<ApiResponse<int>>;

public class BulkToggleStatusCommandValidator : AbstractValidator<BulkToggleStatusCommand>
{
    public BulkToggleStatusCommandValidator()
    {
        RuleFor(x => x.QuestionIds).NotEmpty().Must(ids => ids.Count <= 1000).WithMessage("Max 1000 at once");
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class BulkToggleStatusCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache) : IRequestHandler<BulkToggleStatusCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(BulkToggleStatusCommand request, CancellationToken ct)
    {
        var questions = await db.Questions
            .Where(q => request.QuestionIds.Contains(q.Id))
            .ToListAsync(ct);

        var now = dateTime.UtcNow;
        foreach (var q in questions)
        {
            q.Status = request.Status;
            q.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        await CreateQuestionCommandHandler.InvalidateQuestionCachesAsync(cache, ct);
        return ApiResponse<int>.Ok(questions.Count);
    }
}
