using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Features.Auth;

public record UpdateProfileCommand(
    string? FirstName,
    string? LastName,
    Language? PreferredLanguage) : IRequest<ApiResponse<CurrentUserDto>>;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .When(x => x.FirstName is not null);

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .When(x => x.LastName is not null);

        RuleFor(x => x.PreferredLanguage)
            .IsInEnum()
            .When(x => x.PreferredLanguage is not null);
    }
}

public class UpdateProfileCommandHandler(
    ICurrentUser currentUser,
    IApplicationDbContext db,
    IDateTimeProvider dateTime) : IRequestHandler<UpdateProfileCommand, ApiResponse<CurrentUserDto>>
{
    public async Task<ApiResponse<CurrentUserDto>> Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is null)
            return ApiResponse<CurrentUserDto>.Fail("UNAUTHORIZED", "Not authenticated.");

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);

        if (user is null)
            return ApiResponse<CurrentUserDto>.Fail("USER_NOT_FOUND", "User not found.");

        if (request.FirstName is not null)
            user.FirstName = request.FirstName;

        if (request.LastName is not null)
            user.LastName = request.LastName;

        if (request.PreferredLanguage is not null)
            user.PreferredLanguage = request.PreferredLanguage.Value;

        user.UpdatedAt = dateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        var now = dateTime.UtcNow;
        var hasSubscription = await db.Subscriptions
            .AnyAsync(s => s.UserId == user.Id
                && s.Status == SubscriptionStatus.Active
                && s.ExpiresAt > now, ct);

        return ApiResponse<CurrentUserDto>.Ok(new CurrentUserDto(
            user.Id,
            user.PhoneNumber,
            user.FirstName,
            user.LastName,
            user.Role,
            user.PreferredLanguage,
            hasSubscription));
    }
}
