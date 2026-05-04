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
    LocalizedText Name,
    LocalizedText Description,
    string Slug,
    string? IconUrl,
    Guid? ParentId,
    int SortOrder,
    bool IsActive = true) : IRequest<ApiResponse<Guid>>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotNull();
        RuleFor(x => x.Name.Uz).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Name.UzLatin).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Name.Ru).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
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
        var slugExists = await db.Categories.AnyAsync(c => c.Slug == request.Slug, ct);
        if (slugExists)
            return ApiResponse<Guid>.Fail("SLUG_DUPLICATE", $"Category with slug '{request.Slug}' already exists.");

        if (request.ParentId.HasValue)
        {
            var parentExists = await db.Categories.AnyAsync(c => c.Id == request.ParentId.Value, ct);
            if (!parentExists)
                return ApiResponse<Guid>.Fail("PARENT_NOT_FOUND", "Parent category not found.");
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
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
        await cache.RemoveAsync("avtolider:categories:tree:all", ct);
        await cache.RemoveAsync("avtolider:categories:tree:all:admin", ct);
    }
}
