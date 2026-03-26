using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Questions;

public record ToggleQuestionStatusCommand(Guid QuestionId, QuestionStatus Status) : IRequest<ApiResponse>;

public class ToggleQuestionStatusCommandValidator : AbstractValidator<ToggleQuestionStatusCommand>
{
    public ToggleQuestionStatusCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class ToggleQuestionStatusCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<ToggleQuestionStatusCommandHandler> logger) : IRequestHandler<ToggleQuestionStatusCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(ToggleQuestionStatusCommand request, CancellationToken ct)
    {
        var question = await db.Questions.FindAsync([request.QuestionId], ct);
        if (question is null)
            return ApiResponse.Fail("QUESTION_NOT_FOUND", "Question not found.");

        question.Status = request.Status;
        question.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await CreateQuestionCommandHandler.InvalidateQuestionCachesAsync(cache, ct);

        logger.LogInformation("Question {Id} status set to {Status}", request.QuestionId, request.Status);
        return ApiResponse.Ok();
    }
}
