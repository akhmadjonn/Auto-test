using AutoTest.Application.Common.Interfaces;
using AutoTest.Domain.Common.ValueObjects;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("autotest");

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("phone_number IS NOT NULL");
            e.HasIndex(u => u.TelegramId).IsUnique().HasFilter("telegram_id IS NOT NULL");
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
        });

        // Category
        modelBuilder.Entity<Category>(e =>
        {
            e.OwnsOne(c => c.Name, b => b.ToJson());
            e.OwnsOne(c => c.Description, b => b.ToJson());
            e.HasIndex(c => c.Slug).IsUnique();
            e.Property(c => c.Slug).HasMaxLength(100);
            e.Property(c => c.IconUrl).HasMaxLength(500);
            e.HasOne(c => c.Parent).WithMany(c => c.Children).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        // Question
        modelBuilder.Entity<Question>(e =>
        {
            e.OwnsOne(q => q.Text, b => b.ToJson());
            e.OwnsOne(q => q.Explanation, b => b.ToJson());
            e.Property(q => q.ImageUrl).HasMaxLength(500);
            e.Property(q => q.ThumbnailUrl).HasMaxLength(500);
            e.HasIndex(q => new { q.CategoryId, q.Difficulty }).HasFilter("is_active = true");
            e.HasIndex(q => q.TicketNumber);
            e.HasOne(q => q.Category).WithMany(c => c.Questions).HasForeignKey(q => q.CategoryId);
            e.HasMany(q => q.Tags).WithMany(t => t.Questions).UsingEntity("question_tags");
        });

        // AnswerOption
        modelBuilder.Entity<AnswerOption>(e =>
        {
            e.OwnsOne(a => a.Text, b => b.ToJson());
            e.Property(a => a.ImageUrl).HasMaxLength(500);
            e.HasOne(a => a.Question).WithMany(q => q.AnswerOptions).HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        // Tag
        modelBuilder.Entity<Tag>(e =>
        {
            e.OwnsOne(t => t.Name, b => b.ToJson());
            e.HasIndex(t => t.Slug).IsUnique();
            e.Property(t => t.Slug).HasMaxLength(100);
        });

        // ExamTemplate
        modelBuilder.Entity<ExamTemplate>(e =>
        {
            e.OwnsOne(t => t.Title, b => b.ToJson());
        });

        // ExamPoolRule
        modelBuilder.Entity<ExamPoolRule>(e =>
        {
            e.HasOne(r => r.ExamTemplate).WithMany(t => t.PoolRules).HasForeignKey(r => r.ExamTemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId);
        });

        // ExamSession
        modelBuilder.Entity<ExamSession>(e =>
        {
            e.HasOne(s => s.User).WithMany(u => u.ExamSessions).HasForeignKey(s => s.UserId);
            e.HasOne(s => s.ExamTemplate).WithMany().HasForeignKey(s => s.ExamTemplateId);
            e.HasIndex(s => new { s.UserId, s.Status });
            e.HasIndex(s => new { s.UserId, s.Mode });
        });

        // SessionQuestion
        modelBuilder.Entity<SessionQuestion>(e =>
        {
            e.HasOne(sq => sq.ExamSession).WithMany(s => s.SessionQuestions).HasForeignKey(sq => sq.ExamSessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(sq => sq.Question).WithMany().HasForeignKey(sq => sq.QuestionId);
            e.HasOne(sq => sq.SelectedAnswer).WithMany().HasForeignKey(sq => sq.SelectedAnswerId).OnDelete(DeleteBehavior.SetNull);
        });

        // UserQuestionState — composite key
        modelBuilder.Entity<UserQuestionState>(e =>
        {
            e.HasKey(uqs => new { uqs.UserId, uqs.QuestionId });
            e.HasOne(uqs => uqs.User).WithMany(u => u.UserQuestionStates).HasForeignKey(uqs => uqs.UserId);
            e.HasOne(uqs => uqs.Question).WithMany().HasForeignKey(uqs => uqs.QuestionId);
        });

        // UserCategoryStat — composite key
        modelBuilder.Entity<UserCategoryStat>(e =>
        {
            e.HasKey(ucs => new { ucs.UserId, ucs.CategoryId });
            e.HasOne(ucs => ucs.User).WithMany().HasForeignKey(ucs => ucs.UserId);
            e.HasOne(ucs => ucs.Category).WithMany().HasForeignKey(ucs => ucs.CategoryId);
        });

        // SubscriptionPlan
        modelBuilder.Entity<SubscriptionPlan>(e =>
        {
            e.OwnsOne(p => p.Name, b => b.ToJson());
            e.OwnsOne(p => p.Description, b => b.ToJson());
            e.Property(p => p.Features).HasMaxLength(4000);
        });

        // Subscription
        modelBuilder.Entity<Subscription>(e =>
        {
            e.HasOne(s => s.User).WithMany(u => u.Subscriptions).HasForeignKey(s => s.UserId);
            e.HasOne(s => s.Plan).WithMany().HasForeignKey(s => s.PlanId);
            e.Property(s => s.CardToken).HasMaxLength(500);
        });

        // PaymentTransaction
        modelBuilder.Entity<PaymentTransaction>(e =>
        {
            e.HasOne(pt => pt.User).WithMany().HasForeignKey(pt => pt.UserId);
            e.HasOne(pt => pt.Subscription).WithMany().HasForeignKey(pt => pt.SubscriptionId);
            e.Property(pt => pt.ProviderTransactionId).HasMaxLength(200);
            e.Property(pt => pt.Currency).HasMaxLength(10).HasDefaultValue("UZS");
            e.HasIndex(pt => new { pt.UserId, pt.Status });
        });

        // OtpRequest
        modelBuilder.Entity<OtpRequest>(e =>
        {
            e.Property(o => o.PhoneNumber).HasMaxLength(20);
            e.Property(o => o.CodeHash).HasMaxLength(200);
            e.HasIndex(o => new { o.PhoneNumber, o.IsVerified });
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.EntityType).HasMaxLength(100);
            e.Property(a => a.EntityId).HasMaxLength(100);
            e.Property(a => a.IpAddress).HasMaxLength(50);
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.CreatedAt);
        });

        // SystemSetting
        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(100);
            e.Property(s => s.Value).HasMaxLength(1000);
            e.Property(s => s.Description).HasMaxLength(500);
            e.Property(s => s.UpdatedBy).HasMaxLength(200);
        });

        // UserDevice
        modelBuilder.Entity<UserDevice>(e =>
        {
            e.Property(d => d.DeviceId).HasMaxLength(500);
            e.Property(d => d.DeviceName).HasMaxLength(200);
            e.Property(d => d.FcmToken).HasMaxLength(500);
            e.HasOne(d => d.User).WithMany(u => u.Devices).HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => new { d.UserId, d.DeviceId }).IsUnique();
        });

        // Announcement
        modelBuilder.Entity<Announcement>(e =>
        {
            e.OwnsOne(a => a.Title, b => b.ToJson());
            e.OwnsOne(a => a.Content, b => b.ToJson());
            e.Property(a => a.CreatedBy).HasMaxLength(200);
            e.HasIndex(a => new { a.IsActive, a.StartsAt, a.ExpiresAt });
        });

        base.OnModelCreating(modelBuilder);
    }
}
