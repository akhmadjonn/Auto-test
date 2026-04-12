using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadSigns;

public record UpdateRoadSignCommand(
    Guid Id,
    Guid CategoryId,
    string SignCode,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string? DescriptionUz,
    string? DescriptionUzLatin,
    string? DescriptionRu,
    int SortOrder,
    bool IsActive) : IRequest<ApiResponse>;

public class UpdateRoadSignCommandValidator : AbstractValidator<UpdateRoadSignCommand>
{
    public UpdateRoadSignCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.SignCode).NotEmpty().MaximumLength(20)
            .Matches(@"^[\d.]+$").WithMessage("Sign code must contain only digits and dots.");
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(300);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(300);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(300);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateRoadSignCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UpdateRoadSignCommandHandler> logger) : IRequestHandler<UpdateRoadSignCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateRoadSignCommand request, CancellationToken ct)
    {
        var sign = await db.RoadSigns.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (sign is null)
            return ApiResponse.Fail("NOT_FOUND", "Road sign not found.");

        var codeExists = await db.RoadSigns.AnyAsync(s => s.SignCode == request.SignCode && s.Id != request.Id, ct);
        if (codeExists)
            return ApiResponse.Fail("CODE_DUPLICATE", $"Road sign with code '{request.SignCode}' already exists.");

        var oldCategoryId = sign.CategoryId;

        sign.CategoryId = request.CategoryId;
        sign.SignCode = request.SignCode;
        sign.Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu);
        sign.Description = request.DescriptionUzLatin is not null
            ? new LocalizedText(request.DescriptionUz ?? "", request.DescriptionUzLatin, request.DescriptionRu ?? "")
            : null;
        sign.SortOrder = request.SortOrder;
        sign.IsActive = request.IsActive;
        sign.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateRoadSignCategoryCommandHandler.InvalidateRoadSignCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:road-signs:category:{oldCategoryId}", ct);
        await cache.RemoveAsync($"avtolider:road-signs:category:{request.CategoryId}", ct);
        await cache.RemoveAsync($"avtolider:road-signs:{request.Id}", ct);

        logger.LogInformation("Road sign updated: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
