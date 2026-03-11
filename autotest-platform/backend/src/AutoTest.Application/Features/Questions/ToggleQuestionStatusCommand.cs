using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Questions;

public record ToggleQuestionStatusCommand(Guid QuestionId, bool IsActive) : IRequest<ApiResponse>;

public class ToggleQuestionStatusCommandValidator : AbstractValidator<ToggleQuestionStatusCommand>
{
    public ToggleQuestionStatusCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
    }
}

public class ToggleQuestionStatusCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<ToggleQuestionStatusCommandHandler> logger) : IRequestHandler<ToggleQuestionStatusCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(ToggleQuestionStatusCommand request, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([request.QuestionId], ct);
        if (question is null)
            return ApiResponse.Fail("QUESTION_NOT_FOUND", "Question not found.");

        question.IsActive = request.IsActive;
        question.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Question {Id} status set to {Status}", request.QuestionId, request.IsActive);
        return ApiResponse.Ok();
    }
}
