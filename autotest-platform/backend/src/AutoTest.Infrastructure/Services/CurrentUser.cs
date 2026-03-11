using System.Security.Claims;
using AutoTest.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AutoTest.Infrastructure.Services;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin => httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;
}
