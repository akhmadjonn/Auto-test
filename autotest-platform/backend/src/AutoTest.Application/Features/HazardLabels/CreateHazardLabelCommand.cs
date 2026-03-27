using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.HazardLabels;

public record CreateHazardLabelCommand(
    string Slug,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string DescriptionUz,
    string DescriptionUzLatin,
    string DescriptionRu,
    string HazardClass,
    int SortOrder) : IRequest<ApiResponse<Guid>>;

public class CreateHazardLabelCommandValidator : AbstractValidator<CreateHazardLabelCommand>
{
    public CreateHazardLabelCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DescriptionUz).NotEmpty();
        RuleFor(x => x.DescriptionUzLatin).NotEmpty();
        RuleFor(x => x.DescriptionRu).NotEmpty();
        RuleFor(x => x.HazardClass).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class CreateHazardLabelCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<CreateHazardLabelCommandHandler> logger) : IRequestHandler<CreateHazardLabelCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateHazardLabelCommand request, CancellationToken ct)
    {
        var slugExists = await db.HazardLabels.AnyAsync(l => l.Slug == request.Slug, ct);
        if (slugExists)
            return ApiResponse<Guid>.Fail("SLUG_DUPLICATE", $"Hazard label with slug '{request.Slug}' already exists.");

        var label = new HazardLabel
        {
            Id = Guid.NewGuid(),
            Slug = request.Slug,
            Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu),
            Description = new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu),
            HazardClass = request.HazardClass,
            SortOrder = request.SortOrder,
            CreatedAt = dateTime.UtcNow
        };

        db.HazardLabels.Add(label);
        await db.SaveChangesAsync(ct);
        await InvalidateHazardLabelCacheAsync(cache, ct);

        logger.LogInformation("Hazard label created: {Id} slug={Slug}", label.Id, request.Slug);
        return ApiResponse<Guid>.Ok(label.Id);
    }

    internal static async Task InvalidateHazardLabelCacheAsync(ICacheService cache, CancellationToken ct)
    {
        await cache.RemoveAsync("avtolider:hazard-labels:all", ct);
    }
}
