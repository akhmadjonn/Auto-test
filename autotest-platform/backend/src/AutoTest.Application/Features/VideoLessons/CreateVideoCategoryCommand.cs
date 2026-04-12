using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record CreateVideoCategoryCommand(
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string? DescriptionUz,
    string? DescriptionUzLatin,
    string? DescriptionRu,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateVideoCategoryCommandValidator : AbstractValidator<CreateVideoCategoryCommand>
{
    public CreateVideoCategoryCommandValidator()
    {
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(500);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(500);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(500);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateVideoCategoryCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ICacheService cache,
    ILogger<CreateVideoCategoryCommandHandler> logger) : IRequestHandler<CreateVideoCategoryCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateVideoCategoryCommand request, CancellationToken ct)
    {
        var description = request.DescriptionUz is not null && request.DescriptionUzLatin is not null && request.DescriptionRu is not null
            ? new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu)
            : null;

        var now = dateTime.UtcNow;
        var category = new VideoCategory
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu),
            Description = description,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.VideoCategories.Add(category);
        await db.SaveChangesAsync(ct);
        await InvalidateVideoCategoryCacheAsync(cache, ct);

        logger.LogInformation("Video category created: {Id}", category.Id);
        return ApiResponse<Guid>.Ok(category.Id);
    }

    public static async Task InvalidateVideoCategoryCacheAsync(ICacheService cache, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:video-categories:all", ct);
        await cache.RemoveAsync("avtolider:video-categories:admin", ct);
    }
}
