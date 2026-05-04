using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Glossary;

public record UpdateGlossaryCategoryCommand(
    Guid Id,
    LocalizedText Name,
    string Slug,
    string? Icon,
    int SortOrder) : IRequest<ApiResponse>;

public class UpdateGlossaryCategoryCommandValidator : AbstractValidator<UpdateGlossaryCategoryCommand>
{
    public UpdateGlossaryCategoryCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotNull();
        RuleFor(x => x.Name.Uz).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Name.UzLatin).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Name.Ru).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateGlossaryCategoryCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UpdateGlossaryCategoryCommandHandler> logger) : IRequestHandler<UpdateGlossaryCategoryCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateGlossaryCategoryCommand request, CancellationToken ct)
    {
        var category = await db.GlossaryCategories.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (category is null)
            return ApiResponse.Fail("CATEGORY_NOT_FOUND", "Glossary category not found.");

        var slugExists = await db.GlossaryCategories.AnyAsync(c => c.Slug == request.Slug && c.Id != request.Id, ct);
        if (slugExists)
            return ApiResponse.Fail("SLUG_DUPLICATE", $"Glossary category with slug '{request.Slug}' already exists.");

        category.Name = request.Name;
        category.Slug = request.Slug;
        category.Icon = request.Icon;
        category.SortOrder = request.SortOrder;
        category.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await GetGlossaryCategoriesQueryHandler.InvalidateGlossaryCategoryCacheAsync(cache, ct);
        logger.LogInformation("Glossary category updated: {CategoryId}", request.Id);

        return ApiResponse.Ok();
    }
}
