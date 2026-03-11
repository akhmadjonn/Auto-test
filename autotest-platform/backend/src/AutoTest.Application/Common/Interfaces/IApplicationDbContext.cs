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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
