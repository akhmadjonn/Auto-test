using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Categories;

public record UpdateCategoryCommand(
    Guid Id,
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
    bool IsActive) : IRequest<ApiResponse>;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateCategoryCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UpdateCategoryCommandHandler> logger) : IRequestHandler<UpdateCategoryCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateCategoryCommand request, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (category is null)
            return ApiResponse.Fail("CATEGORY_NOT_FOUND", "Category not found.");

        // Check slug uniqueness (exclude self)
        var slugExists = await db.Categories.AnyAsync(c => c.Slug == request.Slug && c.Id != request.Id, ct);
        if (slugExists)
            return ApiResponse.Fail("SLUG_DUPLICATE", $"Category with slug '{request.Slug}' already exists.");

        // Prevent circular ParentId
        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == request.Id)
                return ApiResponse.Fail("CIRCULAR_PARENT", "A category cannot be its own parent.");

            var parentExists = await db.Categories.AnyAsync(c => c.Id == request.ParentId.Value, ct);
            if (!parentExists)
                return ApiResponse.Fail("PARENT_NOT_FOUND", "Parent category not found.");

            // Check deeper circular reference: walk up the parent chain
            if (await IsDescendantAsync(db, request.ParentId.Value, request.Id, ct))
                return ApiResponse.Fail("CIRCULAR_PARENT", "Setting this parent would create a circular reference.");
        }

        category.Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu);
        category.Description = new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu);
        category.Slug = request.Slug;
        category.IconUrl = request.IconUrl;
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await CreateCategoryCommandHandler.InvalidateCategoryCacheAsync(cache, ct);
        logger.LogInformation("Category updated: {CategoryId}", request.Id);

        return ApiResponse.Ok();
    }

    private static async Task<bool> IsDescendantAsync(
        IApplicationDbContext db, Guid parentId, Guid targetId, CancellationToken ct)
    {
        // Walk up the tree from parentId; if we reach targetId, it's circular
        var currentId = parentId;
        var visited = new HashSet<Guid>();

        while (true)
        {
            if (!visited.Add(currentId))
                return false; // cycle detected in existing data, bail out

            var parent = await db.Categories
                .Where(c => c.Id == currentId)
                .Select(c => c.ParentId)
                .FirstOrDefaultAsync(ct);

            if (parent is null)
                return false;

            if (parent.Value == targetId)
                return true;

            currentId = parent.Value;
        }
    }
}
