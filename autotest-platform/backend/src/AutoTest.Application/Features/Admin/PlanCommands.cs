using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Admin;

// DTOs
public record AdminPlanDto(
    Guid Id, string NameUz, string NameUzLatin, string NameRu,
    string DescriptionUz, string DescriptionUzLatin, string DescriptionRu,
    long PriceInTiyins, int DurationDays, string Features, bool IsActive,
    DateTimeOffset CreatedAt);

// GET all plans (admin view includes inactive)
public record GetAdminPlansQuery : IRequest<ApiResponse<List<AdminPlanDto>>>;

public class GetAdminPlansQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetAdminPlansQuery, ApiResponse<List<AdminPlanDto>>>
{
    public async Task<ApiResponse<List<AdminPlanDto>>> Handle(GetAdminPlansQuery request, CancellationToken ct)
    {
        var plans = await db.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(p => p.PriceInTiyins)
            .Select(p => new AdminPlanDto(
                p.Id, p.Name.Uz, p.Name.UzLatin, p.Name.Ru,
                p.Description.Uz, p.Description.UzLatin, p.Description.Ru,
                p.PriceInTiyins, p.DurationDays, p.Features, p.IsActive, p.CreatedAt))
            .ToListAsync(ct);

        return ApiResponse<List<AdminPlanDto>>.Ok(plans);
    }
}

// CREATE plan
public record CreatePlanCommand(
    string NameUz, string NameUzLatin, string NameRu,
    string DescriptionUz, string DescriptionUzLatin, string DescriptionRu,
    long PriceInTiyins, int DurationDays, string Features, bool IsActive) : IRequest<ApiResponse<AdminPlanDto>>;

public class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanCommandValidator()
    {
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameUzLatin).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameRu).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PriceInTiyins).GreaterThan(0);
        RuleFor(x => x.DurationDays).GreaterThan(0);
    }
}

public class CreatePlanCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<CreatePlanCommandHandler> logger) : IRequestHandler<CreatePlanCommand, ApiResponse<AdminPlanDto>>
{
    public async Task<ApiResponse<AdminPlanDto>> Handle(CreatePlanCommand request, CancellationToken ct)
    {
        var now = dateTime.UtcNow;
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu),
            Description = new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu),
            PriceInTiyins = request.PriceInTiyins,
            DurationDays = request.DurationDays,
            Features = request.Features,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Plan created: {PlanId}", plan.Id);
        return ApiResponse<AdminPlanDto>.Ok(new AdminPlanDto(
            plan.Id, request.NameUz, request.NameUzLatin, request.NameRu,
            request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu,
            plan.PriceInTiyins, plan.DurationDays, plan.Features, plan.IsActive, now));
    }
}

// UPDATE plan
public record UpdatePlanCommand(
    Guid Id,
    string NameUz, string NameUzLatin, string NameRu,
    string DescriptionUz, string DescriptionUzLatin, string DescriptionRu,
    long PriceInTiyins, int DurationDays, string Features) : IRequest<ApiResponse>;

public class UpdatePlanCommandValidator : AbstractValidator<UpdatePlanCommand>
{
    public UpdatePlanCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NameUz).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PriceInTiyins).GreaterThan(0);
        RuleFor(x => x.DurationDays).GreaterThan(0);
    }
}

public class UpdatePlanCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<UpdatePlanCommandHandler> logger) : IRequestHandler<UpdatePlanCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdatePlanCommand request, CancellationToken ct)
    {
        var plan = await db.SubscriptionPlans.FindAsync([request.Id], ct);
        if (plan is null)
            return ApiResponse.Fail("PLAN_NOT_FOUND", "Plan not found.");

        plan.Name = new LocalizedText(request.NameUz, request.NameUzLatin, request.NameRu);
        plan.Description = new LocalizedText(request.DescriptionUz, request.DescriptionUzLatin, request.DescriptionRu);
        plan.PriceInTiyins = request.PriceInTiyins;
        plan.DurationDays = request.DurationDays;
        plan.Features = request.Features;
        plan.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Plan updated: {PlanId}", plan.Id);
        return ApiResponse.Ok();
    }
}

// TOGGLE plan status
public record TogglePlanStatusCommand(Guid Id, bool IsActive) : IRequest<ApiResponse>;

public class TogglePlanStatusCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<TogglePlanStatusCommandHandler> logger) : IRequestHandler<TogglePlanStatusCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(TogglePlanStatusCommand request, CancellationToken ct)
    {
        var plan = await db.SubscriptionPlans.FindAsync([request.Id], ct);
        if (plan is null)
            return ApiResponse.Fail("PLAN_NOT_FOUND", "Plan not found.");

        plan.IsActive = request.IsActive;
        plan.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Plan {PlanId} status set to {Status}", plan.Id, request.IsActive);
        return ApiResponse.Ok();
    }
}
