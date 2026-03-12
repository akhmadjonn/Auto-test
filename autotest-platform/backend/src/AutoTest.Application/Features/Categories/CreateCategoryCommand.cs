using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Categories;

public record CreateCategoryCommand(
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string DescriptionUz,
    string DescriptionUzLatin,
    string DescriptionRu,
    string Slug,
    string? IconUrl,
    Guid? ParentId,
    int SortOrder,
    bool IsActive = true) : IRequest<ApiResponse<Guid>>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateCategoryCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<CreateCategoryCommandHandler> logger) : IRequestHandler<CreateCategoryCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        // Check slug uniqueness
        var slugExists = await db.Categories.AnyAsync(c => c.Slug == request.Slug, ct);
        if (slugExists)
            return ApiResponse<Guid>.Fail("SLUG_DUPLICATE", $"Category with slug '{request.Slug}' already exists.");

        // Validate parent exists if provided
        if (request.ParentId.HasValue)
        {
            var parentExists = await db.Categories.AnyAsync(c => c.Id == request.ParentId.Value, ct);
            if (!parentExists)
                return ApiResponse<Guid>.Fail("PARENT_NOT_FOUND", "Parent category not found.");
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu),
            Description = new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu),
            Slug = request.Slug,
            IconUrl = request.IconUrl,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = dateTime.UtcNow
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        await InvalidateCategoryCacheAsync(cache, ct);
        logger.LogInformation("Category created: {CategoryId} slug={Slug}", category.Id, request.Slug);

        return ApiResponse<Guid>.Ok(category.Id);
    }

    internal static async Task InvalidateCategoryCacheAsync(ICacheService cache, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:categories:tree:Uz", ct);
        await cache.RemoveAsync("avtolider:categories:tree:UzLatin", ct);
        await cache.RemoveAsync("avtolider:categories:tree:Ru", ct);
    }
}
