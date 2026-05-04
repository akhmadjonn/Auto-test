using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Questions;

// Soft delete — flips Status to Inactive so the question disappears from exam
// pools, practice, marathon, and ticket queries (all filter on Status=Active).
// Row stays in DB so user history (SessionQuestion / UserFavoriteQuestion /
// UserQuestionState foreign keys) stays intact. Use the /permanent endpoint
// for irreversible removal (DB row + MinIO images).
public record DeleteQuestionCommand(Guid QuestionId) : IRequest<ApiResponse>;

public class DeleteQuestionCommandValidator : AbstractValidator<DeleteQuestionCommand>
{
    public DeleteQuestionCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
    }
}

public class DeleteQuestionCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<DeleteQuestionCommandHandler> logger) : IRequestHandler<DeleteQuestionCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteQuestionCommand request, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([request.QuestionId], ct);
        if (question is null)
            return ApiResponse.Fail("QUESTION_NOT_FOUND", "Question not found.");

        question.Status = QuestionStatus.Inactive;
        question.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await CreateQuestionCommandHandler.InvalidateQuestionCachesAsync(cache, ct);

        logger.LogInformation("Soft-deleted question {Id} (Status=Inactive)", request.QuestionId);
        return ApiResponse.Ok();
    }
}
