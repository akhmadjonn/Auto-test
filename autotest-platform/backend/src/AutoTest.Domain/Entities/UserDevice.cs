using AutoTest.Domain.Common.Enums;

namespace AutoTest.Domain.Entities;

public class UserDevice : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public DevicePlatform Platform { get; set; }
    public string DeviceId { get; set; } = null!;
    public string? DeviceName { get; set; }
    public string? FcmToken { get; set; }
    public DateTimeOffset? LastActiveAt { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
}
