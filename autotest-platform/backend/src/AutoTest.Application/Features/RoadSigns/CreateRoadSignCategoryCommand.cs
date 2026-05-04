using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record CreateRoadSignCategoryCommand(
    string Slug,
    string Code,
    LocalizedText Name,
    LocalizedText Description,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateRoadSignCategoryCommandValidator : AbstractValidator<CreateRoadSignCategoryCommand>
{
    public CreateRoadSignCategoryCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Name).NotNull();
        RuleFor(x => x.Name.Uz).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Name.UzLatin).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Name.Ru).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Description).NotNull();
        RuleFor(x => x.Description.Uz).NotEmpty().When(x => x.Description is not null);
        RuleFor(x => x.Description.UzLatin).NotEmpty().When(x => x.Description is not null);
        RuleFor(x => x.Description.Ru).NotEmpty().When(x => x.Description is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateRoadSignCategoryCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<CreateRoadSignCategoryCommandHandler> logger) : IRequestHandler<CreateRoadSignCategoryCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateRoadSignCategoryCommand request, CancellationToken ct)
    {
        var slugExists = await db.RoadSignCategories.AnyAsync(c => c.Slug == request.Slug, ct);
        if (slugExists)
            return ApiResponse<Guid>.Fail("SLUG_DUPLICATE", $"Road sign category with slug '{request.Slug}' already exists.");

        var codeExists = await db.RoadSignCategories.AnyAsync(c => c.Code == request.Code, ct);
        if (codeExists)
            return ApiResponse<Guid>.Fail("CODE_DUPLICATE", $"Road sign category with code '{request.Code}' already exists.");

        var category = new RoadSignCategory
        {
            Id = Guid.NewGuid(),
            Slug = request.Slug,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = dateTime.UtcNow
        };

        db.RoadSignCategories.Add(category);
        await db.SaveChangesAsync(ct);
        await InvalidateRoadSignCacheAsync(cache, ct);

        logger.LogInformation("Road sign category created: {Id} slug={Slug}", category.Id, request.Slug);
        return ApiResponse<Guid>.Ok(category.Id);
    }

    internal static async Task InvalidateRoadSignCacheAsync(ICacheService cache, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:road-sign-categories:all", ct);
        await cache.RemoveAsync("avtolider:road-signs:all", ct);
    }
}
