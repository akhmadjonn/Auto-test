using AutoTest.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Question> Questions { get; }
    DbSet<AnswerOption> AnswerOptions { get; }
    DbSet<Tag> Tags { get; }
    DbSet<ExamTemplate> ExamTemplates { get; }
    DbSet<ExamPoolRule> ExamPoolRules { get; }
    DbSet<ExamSession> ExamSessions { get; }
    DbSet<SessionQuestion> SessionQuestions { get; }
    DbSet<UserQuestionState> UserQuestionStates { get; }
    DbSet<UserCategoryStat> UserCategoryStats { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<OtpRequest> OtpRequests { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<UserDevice> UserDevices { get; }

    // Phase 2 — Engagement
    DbSet<UserDailyStats> UserDailyStats { get; }
    DbSet<UserFavoriteQuestion> UserFavoriteQuestions { get; }
    DbSet<UserSetting> UserSettings { get; }
    DbSet<LeaderboardSnapshot> LeaderboardSnapshots { get; }

    // Phase 2 — Reference content
    DbSet<TrafficFine> TrafficFines { get; }
    DbSet<HazardLabel> HazardLabels { get; }
    DbSet<FirstAidProcedure> FirstAidProcedures { get; }
    DbSet<FirstAidStep> FirstAidSteps { get; }
    DbSet<GlossaryCategory> GlossaryCategories { get; }
    DbSet<GlossaryTerm> GlossaryTerms { get; }
    DbSet<ColorVisionPlate> ColorVisionPlates { get; }

    // Phase 3 — Road Signs & Markings
    DbSet<RoadSignCategory> RoadSignCategories { get; }
    DbSet<RoadSign> RoadSigns { get; }
    DbSet<RoadMarking> RoadMarkings { get; }

    // Phase 4 — Video Lessons
    DbSet<VideoCategory> VideoCategories { get; }
    DbSet<VideoLesson> VideoLessons { get; }
    DbSet<LessonAttachment> LessonAttachments { get; }
    DbSet<UserLessonProgress> UserLessonProgress { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
