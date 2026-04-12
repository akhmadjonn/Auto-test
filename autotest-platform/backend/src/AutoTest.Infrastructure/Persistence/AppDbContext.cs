using System.Reflection;
using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutoTest.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ExamTemplate> ExamTemplates => Set<ExamTemplate>();
    public DbSet<ExamPoolRule> ExamPoolRules => Set<ExamPoolRule>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<SessionQuestion> SessionQuestions => Set<SessionQuestion>();
    public DbSet<UserQuestionState> UserQuestionStates => Set<UserQuestionState>();
    public DbSet<UserCategoryStat> UserCategoryStats => Set<UserCategoryStat>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<OtpRequest> OtpRequests => Set<OtpRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();

    // Phase 2 — Engagement
    public DbSet<UserDailyStats> UserDailyStats => Set<UserDailyStats>();
    public DbSet<UserFavoriteQuestion> UserFavoriteQuestions => Set<UserFavoriteQuestion>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<LeaderboardSnapshot> LeaderboardSnapshots => Set<LeaderboardSnapshot>();

    // Phase 2 — Reference content
    public DbSet<TrafficFine> TrafficFines => Set<TrafficFine>();
    public DbSet<HazardLabel> HazardLabels => Set<HazardLabel>();
    public DbSet<FirstAidProcedure> FirstAidProcedures => Set<FirstAidProcedure>();
    public DbSet<FirstAidStep> FirstAidSteps => Set<FirstAidStep>();
    public DbSet<GlossaryCategory> GlossaryCategories => Set<GlossaryCategory>();
    public DbSet<GlossaryTerm> GlossaryTerms => Set<GlossaryTerm>();
    public DbSet<ColorVisionPlate> ColorVisionPlates => Set<ColorVisionPlate>();

    // Phase 3 — Road Signs & Markings
    public DbSet<RoadSignCategory> RoadSignCategories => Set<RoadSignCategory>();
    public DbSet<RoadSign> RoadSigns => Set<RoadSign>();
    public DbSet<RoadMarking> RoadMarkings => Set<RoadMarking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("autotest");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }
}
