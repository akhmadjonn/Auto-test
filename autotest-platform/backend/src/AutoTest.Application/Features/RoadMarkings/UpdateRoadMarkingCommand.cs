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
    LocalizedText Name,
    LocalizedText? Description,
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
        RuleFor(x => x.Name).NotNull();
        RuleFor(x => x.Name.Uz).NotEmpty().MaximumLength(300).When(x => x.Name is not null);
        RuleFor(x => x.Name.UzLatin).NotEmpty().MaximumLength(300).When(x => x.Name is not null);
        RuleFor(x => x.Name.Ru).NotEmpty().MaximumLength(300).When(x => x.Name is not null);
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
        marking.Name = request.Name;
        marking.Description = request.Description;
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
