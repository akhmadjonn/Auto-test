using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Glossary;

public record CreateGlossaryCategoryCommand(
    LocalizedText Name,
    string Slug,
    string? Icon,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateGlossaryCategoryCommandValidator : AbstractValidator<CreateGlossaryCategoryCommand>
{
    public CreateGlossaryCategoryCommandValidator()
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

public class CreateGlossaryCategoryCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<CreateGlossaryCategoryCommandHandler> logger) : IRequestHandler<CreateGlossaryCategoryCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateGlossaryCategoryCommand request, CancellationToken ct)
    {
        var slugExists = await db.GlossaryCategories.AnyAsync(c => c.Slug == request.Slug, ct);
        if (slugExists)
            return ApiResponse<Guid>.Fail("SLUG_DUPLICATE", $"Glossary category with slug '{request.Slug}' already exists.");

        var category = new GlossaryCategory
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            Icon = request.Icon,
            SortOrder = request.SortOrder,
            CreatedAt = dateTime.UtcNow
        };

        db.GlossaryCategories.Add(category);
        await db.SaveChangesAsync(ct);

        await GetGlossaryCategoriesQueryHandler.InvalidateGlossaryCategoryCacheAsync(cache, ct);
        logger.LogInformation("Glossary category created: {CategoryId} slug={Slug}", category.Id, request.Slug);

        return ApiResponse<Guid>.Ok(category.Id);
    }
}
