using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.VideoLessons;

public record CreateVideoCategoryCommand(
    LocalizedText Name,
    LocalizedText? Description,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateVideoCategoryCommandValidator : AbstractValidator<CreateVideoCategoryCommand>
{
    public CreateVideoCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotNull();
        RuleFor(x => x.Name.Uz).NotEmpty().MaximumLength(500).When(x => x.Name is not null);
        RuleFor(x => x.Name.UzLatin).NotEmpty().MaximumLength(500).When(x => x.Name is not null);
        RuleFor(x => x.Name.Ru).NotEmpty().MaximumLength(500).When(x => x.Name is not null);
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
        var now = dateTime.UtcNow;
        var category = new VideoCategory
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
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
