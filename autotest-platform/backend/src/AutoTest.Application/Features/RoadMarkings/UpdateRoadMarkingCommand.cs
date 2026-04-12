using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.RoadMarkings;

public record UpdateRoadMarkingCommand(
    Guid Id,
    string MarkingCode,
    RoadMarkingType MarkingType,
    string NameUz,
    string NameUzLatin,
    string NameRu,
    string? DescriptionUz,
    string? DescriptionUzLatin,
    string? DescriptionRu,
    int SortOrder,
    bool IsActive) : IRequest<ApiResponse>;

public class UpdateRoadMarkingCommandValidator : AbstractValidator<UpdateRoadMarkingCommand>
{
    public UpdateRoadMarkingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.MarkingCode).NotEmpty().MaximumLength(20)
            .Matches(@"^[\d.]+$").WithMessage("Marking code must contain only digits and dots.");
        RuleFor(x => x.MarkingType).IsInEnum();
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(300);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(300);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(300);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateRoadMarkingCommandHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime,
    ILogger<UpdateRoadMarkingCommandHandler> logger) : IRequestHandler<UpdateRoadMarkingCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateRoadMarkingCommand request, CancellationToken ct)
    {
        var marking = await db.RoadMarkings.FirstOrDefaultAsync(m => m.Id == request.Id, ct);
        if (marking is null)
            return ApiResponse.Fail("NOT_FOUND", "Road marking not found.");

        var codeExists = await db.RoadMarkings.AnyAsync(m => m.MarkingCode == request.MarkingCode && m.Id != request.Id, ct);
        if (codeExists)
            return ApiResponse.Fail("CODE_DUPLICATE", $"Road marking with code '{request.MarkingCode}' already exists.");

        marking.MarkingCode = request.MarkingCode;
        marking.MarkingType = request.MarkingType;
        marking.Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu);
        marking.Description = request.DescriptionUzLatin is not null
            ? new LocalizedText(request.DescriptionUz ?? "", request.DescriptionUzLatin, request.DescriptionRu ?? "")
            : null;
        marking.SortOrder = request.SortOrder;
        marking.IsActive = request.IsActive;
        marking.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await CreateRoadMarkingCommandHandler.InvalidateRoadMarkingCacheAsync(cache, ct);
        await cache.RemoveAsync($"avtolider:road-markings:{request.Id}", ct);

        logger.LogInformation("Road marking updated: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}
