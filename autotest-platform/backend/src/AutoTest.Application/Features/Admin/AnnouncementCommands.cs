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
public record AnnouncementDto(
    Guid Id,
    string TitleUz, string TitleUzLatin, string TitleRu,
    string ContentUz, string ContentUzLatin, string ContentRu,
    AnnouncementType Type, bool IsActive,
    DateTimeOffset? StartsAt, DateTimeOffset? ExpiresAt,
    string? CreatedBy, DateTimeOffset CreatedAt);

// Admin GET all
public record GetAnnouncementsQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PaginatedList<AnnouncementDto>>>;

public class GetAnnouncementsQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetAnnouncementsQuery, ApiResponse<PaginatedList<AnnouncementDto>>>
{
    public async Task<ApiResponse<PaginatedList<AnnouncementDto>>> Handle(GetAnnouncementsQuery request, CancellationToken ct)
    {
        var projected = db.Announcements
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AnnouncementDto(
                a.Id,
                a.Title.Uz, a.Title.UzLatin, a.Title.Ru,
                a.Content.Uz, a.Content.UzLatin, a.Content.Ru,
                a.Type, a.IsActive,
                a.StartsAt, a.ExpiresAt,
                a.CreatedBy, a.CreatedAt));

        var result = await PaginatedList<AnnouncementDto>.CreateAsync(projected, request.Page, request.PageSize, ct);
        return ApiResponse<PaginatedList<AnnouncementDto>>.Ok(result);
    }
}

// CREATE
public record CreateAnnouncementCommand(
    string TitleUz, string TitleUzLatin, string TitleRu,
    string ContentUz, string ContentUzLatin, string ContentRu,
    AnnouncementType Type, bool IsActive,
    DateTimeOffset? StartsAt, DateTimeOffset? ExpiresAt) : IRequest<ApiResponse<AnnouncementDto>>;

public class CreateAnnouncementCommandValidator : AbstractValidator<CreateAnnouncementCommand>
{
    public CreateAnnouncementCommandValidator()
    {
        RuleFor(x => x.TitleUz).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TitleUzLatin).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TitleRu).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ContentUz).NotEmpty();
        RuleFor(x => x.ContentUzLatin).NotEmpty();
        RuleFor(x => x.ContentRu).NotEmpty();
    }
}

public class CreateAnnouncementCommandHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ILogger<CreateAnnouncementCommandHandler> logger) : IRequestHandler<CreateAnnouncementCommand, ApiResponse<AnnouncementDto>>
{
    public async Task<ApiResponse<AnnouncementDto>> Handle(CreateAnnouncementCommand request, CancellationToken ct)
    {
        var now = dateTime.UtcNow;
        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            Title = new LocalizedText(request.TitleUz, request.TitleUzLatin, request.TitleRu),
            Content = new LocalizedText(request.ContentUz, request.ContentUzLatin, request.ContentRu),
            Type = request.Type,
            IsActive = request.IsActive,
            StartsAt = request.StartsAt,
            ExpiresAt = request.ExpiresAt,
            CreatedBy = currentUser.UserId?.ToString(),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Announcement created: {Id}", announcement.Id);
        return ApiResponse<AnnouncementDto>.Ok(new AnnouncementDto(
            announcement.Id,
            request.TitleUz, request.TitleUzLatin, request.TitleRu,
            request.ContentUz, request.ContentUzLatin, request.ContentRu,
            request.Type, request.IsActive,
            request.StartsAt, request.ExpiresAt,
            announcement.CreatedBy, now));
    }
}

// UPDATE
public record UpdateAnnouncementCommand(
    Guid Id,
    string TitleUz, string TitleUzLatin, string TitleRu,
    string ContentUz, string ContentUzLatin, string ContentRu,
    AnnouncementType Type, bool IsActive,
    DateTimeOffset? StartsAt, DateTimeOffset? ExpiresAt) : IRequest<ApiResponse>;

public class UpdateAnnouncementCommandValidator : AbstractValidator<UpdateAnnouncementCommand>
{
    public UpdateAnnouncementCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TitleUz).NotEmpty().MaximumLength(500);
    }
}

public class UpdateAnnouncementCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<UpdateAnnouncementCommandHandler> logger) : IRequestHandler<UpdateAnnouncementCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateAnnouncementCommand request, CancellationToken ct)
    {
        var announcement = await db.Announcements.FindAsync([request.Id], ct);
        if (announcement is null)
            return ApiResponse.Fail("NOT_FOUND", "Announcement not found.");

        announcement.Title = new LocalizedText(request.TitleUz, request.TitleUzLatin, request.TitleRu);
        announcement.Content = new LocalizedText(request.ContentUz, request.ContentUzLatin, request.ContentRu);
        announcement.Type = request.Type;
        announcement.IsActive = request.IsActive;
        announcement.StartsAt = request.StartsAt;
        announcement.ExpiresAt = request.ExpiresAt;
        announcement.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Announcement updated: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}

// DELETE
public record DeleteAnnouncementCommand(Guid Id) : IRequest<ApiResponse>;

public class DeleteAnnouncementCommandHandler(
    IApplicationDbContext db,
    ILogger<DeleteAnnouncementCommandHandler> logger) : IRequestHandler<DeleteAnnouncementCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(DeleteAnnouncementCommand request, CancellationToken ct)
    {
        var announcement = await db.Announcements.FindAsync([request.Id], ct);
        if (announcement is null)
            return ApiResponse.Fail("NOT_FOUND", "Announcement not found.");

        db.Announcements.Remove(announcement);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Announcement deleted: {Id}", request.Id);
        return ApiResponse.Ok();
    }
}

// Public — GET active announcements (cached 5min)
public record GetActiveAnnouncementsQuery(Language Language = Language.UzLatin)
    : IRequest<ApiResponse<List<ActiveAnnouncementDto>>>;

public record ActiveAnnouncementDto(Guid Id, string Title, string Content, AnnouncementType Type, DateTimeOffset? ExpiresAt);

public class GetActiveAnnouncementsQueryHandler(
    IApplicationDbContext db,
    ICacheService cache,
    IDateTimeProvider dateTime) : IRequestHandler<GetActiveAnnouncementsQuery, ApiResponse<List<ActiveAnnouncementDto>>>
{
    private const string CacheKey = "avtolider:announcements:active";

    public async Task<ApiResponse<List<ActiveAnnouncementDto>>> Handle(GetActiveAnnouncementsQuery request, CancellationToken ct)
    {
        var cacheKeyLang = $"{CacheKey}:{request.Language}";
        var cached = await cache.GetAsync<List<ActiveAnnouncementDto>>(cacheKeyLang, ct);
        if (cached is not null)
            return ApiResponse<List<ActiveAnnouncementDto>>.Ok(cached);

        var now = dateTime.UtcNow;
        var announcements = await db.Announcements
            .AsNoTracking()
            .Where(a => a.IsActive
                && (a.StartsAt == null || a.StartsAt <= now)
                && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.Type)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var dtos = announcements.Select(a => new ActiveAnnouncementDto(
            a.Id,
            a.Title.Get(request.Language),
            a.Content.Get(request.Language),
            a.Type,
            a.ExpiresAt)).ToList();

        await cache.SetAsync(cacheKeyLang, dtos, TimeSpan.FromMinutes(5), ct);
        return ApiResponse<List<ActiveAnnouncementDto>>.Ok(dtos);
    }
}
