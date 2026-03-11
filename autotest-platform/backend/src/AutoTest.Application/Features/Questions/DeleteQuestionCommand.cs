using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Questions;

// Soft delete
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
    ILogger<DeleteQuestionCommandHandler> logger) : IRequestHandler<DeleteQuestionCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteQuestionCommand request, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([request.QuestionId], ct);
        if (question is null)
            return ApiResponse.Fail("QUESTION_NOT_FOUND", "Question not found.");

        question.IsActive = false;
        question.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Soft-deleted question {Id}", request.QuestionId);
        return ApiResponse.Ok();
    }
}
