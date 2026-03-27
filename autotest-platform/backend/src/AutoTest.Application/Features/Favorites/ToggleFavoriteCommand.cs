using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Favorites;

public record ToggleFavoriteCommand(Guid QuestionId) : IRequest<ApiResponse<FavoriteToggleDto>>;

public record FavoriteToggleDto(bool IsFavorited);

public class ToggleFavoriteCommandValidator : AbstractValidator<ToggleFavoriteCommand>
{
    public ToggleFavoriteCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
    }
}

public class ToggleFavoriteCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime)
    : IRequestHandler<ToggleFavoriteCommand, ApiResponse<FavoriteToggleDto>>
{
    public async Task<ApiResponse<FavoriteToggleDto>> Handle(ToggleFavoriteCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<FavoriteToggleDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;

        var existing = await db.UserFavoriteQuestions
            .FirstOrDefaultAsync(f => f.UserId == userId && f.QuestionId == request.QuestionId, ct);

        if (existing is not null)
        {
            db.UserFavoriteQuestions.Remove(existing);
            await db.SaveChangesAsync(ct);
            return ApiResponse<FavoriteToggleDto>.Ok(new FavoriteToggleDto(false));
        }

        // Verify question exists
        var questionExists = await db.Questions
            .AnyAsync(q => q.Id == request.QuestionId, ct);
        if (!questionExists)
            return ApiResponse<FavoriteToggleDto>.Fail("QUESTION_NOT_FOUND", "Question not found.");

        db.UserFavoriteQuestions.Add(new UserFavoriteQuestion
        {
            UserId = userId,
            QuestionId = request.QuestionId,
            CreatedAt = dateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return ApiResponse<FavoriteToggleDto>.Ok(new FavoriteToggleDto(true));
    }
}
