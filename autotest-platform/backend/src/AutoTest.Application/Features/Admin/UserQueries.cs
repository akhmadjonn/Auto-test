using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Admin;

// List users
public record GetUsersListQuery(
    string? Search = null,
    UserRole? Role = null,
    bool? IsBlocked = null,
    int Page = 1,
    int PageSize = 20) : IRequest<ApiResponse<PaginatedList<UserListItemDto>>>;

public record UserListItemDto(
    Guid Id, string? PhoneNumber, string? FirstName, string? LastName,
    UserRole Role, AuthProvider AuthProvider, bool IsBlocked,
    DateTimeOffset? LastActiveAt, DateTimeOffset CreatedAt);

public class GetUsersListQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetUsersListQuery, ApiResponse<PaginatedList<UserListItemDto>>>
{
    public async Task<ApiResponse<PaginatedList<UserListItemDto>>> Handle(GetUsersListQuery request, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().AsQueryable();

        if (request.Search is not null)
        {
            var search = request.Search.ToLower();
            query = query.Where(u =>
                (u.PhoneNumber != null && u.PhoneNumber.Contains(search)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(search)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(search)));
        }

        if (request.Role.HasValue)
            query = query.Where(u => u.Role == request.Role.Value);

        if (request.IsBlocked.HasValue)
            query = query.Where(u => u.IsBlocked == request.IsBlocked.Value);

        var projected = query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserListItemDto(
                u.Id, u.PhoneNumber, u.FirstName, u.LastName,
                u.Role, u.AuthProvider, u.IsBlocked,
                u.LastActiveAt, u.CreatedAt));

        var result = await PaginatedList<UserListItemDto>.CreateAsync(projected, request.Page, request.PageSize, ct);
        return ApiResponse<PaginatedList<UserListItemDto>>.Ok(result);
    }
}

// Get user detail (full profile)
public record GetUserDetailQuery(Guid UserId) : IRequest<ApiResponse<UserDetailDto>>;

public record UserDetailDto(
    Guid Id, string? PhoneNumber, string? FirstName, string? LastName,
    UserRole Role, AuthProvider AuthProvider, Language PreferredLanguage,
    long? TelegramId, bool IsBlocked, DateTimeOffset? LastActiveAt, DateTimeOffset CreatedAt,
    int TotalExams, int CompletedExams, int AverageScore,
    SubscriptionInfoDto? ActiveSubscription);

public record SubscriptionInfoDto(Guid PlanId, string PlanName, SubscriptionStatus Status, DateTimeOffset ExpiresAt);

public class GetUserDetailQueryValidator : AbstractValidator<GetUserDetailQuery>
{
    public GetUserDetailQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class GetUserDetailQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetUserDetailQuery, ApiResponse<UserDetailDto>>
{
    public async Task<ApiResponse<UserDetailDto>> Handle(GetUserDetailQuery request, CancellationToken ct)
    {
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

        if (user is null)
            return ApiResponse<UserDetailDto>.Fail("USER_NOT_FOUND", "User not found.");

        var examStats = await db.ExamSessions
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(s => s.Status == ExamStatus.Completed),
                AvgScore = g.Where(s => s.Score.HasValue).Average(s => (double?)s.Score) ?? 0
            })
            .FirstOrDefaultAsync(ct);

        var activeSub = await db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.UserId == request.UserId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.ExpiresAt)
            .FirstOrDefaultAsync(ct);

        var subDto = activeSub is not null
            ? new SubscriptionInfoDto(activeSub.PlanId, activeSub.Plan.Name.UzLatin, activeSub.Status, activeSub.ExpiresAt)
            : null;

        return ApiResponse<UserDetailDto>.Ok(new UserDetailDto(
            user.Id, user.PhoneNumber, user.FirstName, user.LastName,
            user.Role, user.AuthProvider, user.PreferredLanguage,
            user.TelegramId, user.IsBlocked, user.LastActiveAt, user.CreatedAt,
            examStats?.Total ?? 0, examStats?.Completed ?? 0, (int)(examStats?.AvgScore ?? 0),
            subDto));
    }
}

// Update user role
public record UpdateUserRoleCommand(Guid UserId, UserRole Role) : IRequest<ApiResponse>;

public class UpdateUserRoleCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<UpdateUserRoleCommandHandler> logger) : IRequestHandler<UpdateUserRoleCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateUserRoleCommand request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([request.UserId], ct);
        if (user is null)
            return ApiResponse.Fail("USER_NOT_FOUND", "User not found.");

        user.Role = request.Role;
        user.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} role updated to {Role}", request.UserId, request.Role);
        return ApiResponse.Ok();
    }
}

// Block/unblock user
public record ToggleUserBlockCommand(Guid UserId, bool IsBlocked) : IRequest<ApiResponse>;

public class ToggleUserBlockCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<ToggleUserBlockCommandHandler> logger) : IRequestHandler<ToggleUserBlockCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(ToggleUserBlockCommand request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([request.UserId], ct);
        if (user is null)
            return ApiResponse.Fail("USER_NOT_FOUND", "User not found.");

        user.IsBlocked = request.IsBlocked;
        user.UpdatedAt = dateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} blocked: {IsBlocked}", request.UserId, request.IsBlocked);
        return ApiResponse.Ok();
    }
}
