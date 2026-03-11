using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class Announcement : BaseAuditableEntity
{
    public LocalizedText Title { get; set; } = null!;
    public LocalizedText Content { get; set; } = null!;
    public AnnouncementType Type { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? CreatedBy { get; set; }
}
