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
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string Slug,
    string? Icon,
    int SortOrder) : IRequest<ApiResponse>;

public class UpdateGlossaryCategoryCommandValidator : AbstractValidator<UpdateGlossaryCategoryCommand>
{
    public UpdateGlossaryCategoryCommandValidator()
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

        category.Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu);
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
