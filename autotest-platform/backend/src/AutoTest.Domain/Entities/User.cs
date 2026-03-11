using AutoTest.Domain.Common.Enums;

namespace AutoTest.Domain.Entities;

public class User : BaseAuditableEntity
{
    public string? PhoneNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public UserRole Role { get; set; }
    public AuthProvider AuthProvider { get; set; }
    public Language PreferredLanguage { get; set; }
    public long? TelegramId { get; set; }
    public bool IsBlocked { get; set; }
    public DateTimeOffset? LastActiveAt { get; set; }

    public ICollection<ExamSession> ExamSessions { get; set; } = [];
    public ICollection<UserQuestionState> UserQuestionStates { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<UserDevice> Devices { get; set; } = [];
}
