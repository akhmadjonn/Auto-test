using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Questions;

// Hard delete: remove images + cascade DB delete
public record PermanentDeleteQuestionCommand(Guid QuestionId) : IRequest<ApiResponse>;

public class PermanentDeleteQuestionCommandValidator : AbstractValidator<PermanentDeleteQuestionCommand>
{
    public PermanentDeleteQuestionCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
    }
}

public class PermanentDeleteQuestionCommandHandler(
    IApplicationDbContext db,
    IFileStorageService storage,
    ICacheService cache,
    ILogger<PermanentDeleteQuestionCommandHandler> logger) : IRequestHandler<PermanentDeleteQuestionCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(PermanentDeleteQuestionCommand request, CancellationToken ct)
    {
        var question = await db.Questions
            .Include(q => q.AnswerOptions)
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, ct);

        if (question is null)
            return ApiResponse.Fail("QUESTION_NOT_FOUND", "Question not found.");

        // Collect all images for deletion
        var imageKeys = new List<string>();
        if (question.ImageUrl is not null) imageKeys.Add(question.ImageUrl);
        if (question.ThumbnailUrl is not null) imageKeys.Add(question.ThumbnailUrl);
        imageKeys.AddRange(question.AnswerOptions
            .Where(a => a.ImageUrl is not null)
            .Select(a => a.ImageUrl!));

        db.Questions.Remove(question);
        await db.SaveChangesAsync(ct);
        await CreateQuestionCommandHandler.InvalidateQuestionCachesAsync(cache, ct);

        if (imageKeys.Count > 0)
            await storage.DeleteManyAsync(imageKeys, ct);

        logger.LogInformation("Permanently deleted question {Id}", request.QuestionId);
        return ApiResponse.Ok();
    }
}
