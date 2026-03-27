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

    // Engagement / XP system
    public long TotalXp { get; set; }
    public int Level { get; set; } = 1;
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateTimeOffset? LastStudyDate { get; set; }

    public ICollection<ExamSession> ExamSessions { get; set; } = [];
    public ICollection<UserQuestionState> UserQuestionStates { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<UserDevice> Devices { get; set; } = [];
    public ICollection<UserDailyStats> DailyStats { get; set; } = [];
    public ICollection<UserFavoriteQuestion> FavoriteQuestions { get; set; } = [];
    public ICollection<UserSetting> Settings { get; set; } = [];
}
