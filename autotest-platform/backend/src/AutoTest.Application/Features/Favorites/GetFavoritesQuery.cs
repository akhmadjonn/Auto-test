using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Favorites;

public record GetFavoritesQuery(
    int Page = 1,
    int PageSize = 20,
    Language Language = Language.UzLatin) : IRequest<ApiResponse<PaginatedList<FavoriteQuestionDto>>>;

public record FavoriteQuestionDto(
    Guid QuestionId,
    LocalizedText Text,
    string? ImageUrl,
    Difficulty Difficulty,
    DateTimeOffset FavoritedAt);

public class GetFavoritesQueryValidator : AbstractValidator<GetFavoritesQuery>
{
    public GetFavoritesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class GetFavoritesQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IFileStorageService storage)
    : IRequestHandler<GetFavoritesQuery, ApiResponse<PaginatedList<FavoriteQuestionDto>>>
{
    public async Task<ApiResponse<PaginatedList<FavoriteQuestionDto>>> Handle(
        GetFavoritesQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<PaginatedList<FavoriteQuestionDto>>.Fail("UNAUTHORIZED", "Not authenticated.");

        var userId = currentUser.UserId.Value;

        var query = db.UserFavoriteQuestions
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .Join(db.Questions.AsNoTracking(), f => f.QuestionId, q => q.Id,
                (f, q) => new { Favorite = f, Question = q })
            .OrderByDescending(x => x.Favorite.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        // Batch presigned URLs
        var imageKeys = items
            .Where(i => i.Question.ImageUrl is not null)
            .Select(i => i.Question.ImageUrl!)
            .ToList();
        var urlMap = await storage.GetPresignedUrlsBatchAsync(imageKeys, ct);

        var dtos = items.Select(i => new FavoriteQuestionDto(
            i.Question.Id,
            i.Question.Text,
            i.Question.ImageUrl is not null ? urlMap.GetValueOrDefault(i.Question.ImageUrl) : null,
            i.Question.Difficulty,
            i.Favorite.CreatedAt)).ToList();

        var result = new PaginatedList<FavoriteQuestionDto>(dtos, totalCount, request.Page, request.PageSize);

        return ApiResponse<PaginatedList<FavoriteQuestionDto>>.Ok(result);
    }
}
