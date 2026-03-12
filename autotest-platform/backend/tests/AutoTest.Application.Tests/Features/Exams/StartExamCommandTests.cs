using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Features.Exams;
using AutoTest.Application.Tests.TestHelpers;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AutoTest.Application.Tests.Features.Exams;

public class StartExamCommandTests
{
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };
    private readonly FakeCurrentUser _currentUser = new() { UserId = Guid.NewGuid() };
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeCacheService _cache = new();
    private readonly ILogger<StartExamCommandHandler> _logger = Substitute.For<ILogger<StartExamCommandHandler>>();

    private readonly FakeDistributedLockService _lockService = new();

    private StartExamCommandHandler CreateHandler(IApplicationDbContext db) =>
        new(db, _currentUser, _storage, _cache, _lockService, _dateTime, _logger);

    private async Task<(Guid templateId, Guid categoryId)> SeedTemplateWithQuestions(IApplicationDbContext db, int questionCount = 10)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("Test", "Test", "Test"),
            Description = new LocalizedText("Test", "Test", "Test"),
            Slug = "test-cat",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.Categories.Add(category);

        var template = new ExamTemplate
        {
            Id = Guid.NewGuid(),
            Title = new LocalizedText("Test Exam", "Test Exam", "Test Exam"),
            TotalQuestions = questionCount,
            PassingScore = 80,
            TimeLimitMinutes = 20,
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.ExamTemplates.Add(template);

        var poolRule = new ExamPoolRule
        {
            Id = Guid.NewGuid(),
            ExamTemplateId = template.Id,
            CategoryId = category.Id,
            QuestionCount = questionCount,
            CreatedAt = _dateTime.UtcNow
        };
        db.ExamPoolRules.Add(poolRule);

        for (var i = 0; i < questionCount + 5; i++)
        {
            var q = new Question
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Text = new LocalizedText($"Q{i}", $"Q{i}", $"Q{i}"),
                Explanation = new LocalizedText("Expl", "Expl", "Expl"),
                Difficulty = Difficulty.Easy,
                TicketNumber = 1,
                LicenseCategory = LicenseCategory.AB,
                IsActive = true,
                CreatedAt = _dateTime.UtcNow
            };
            db.Questions.Add(q);

            for (var j = 0; j < 4; j++)
            {
                db.AnswerOptions.Add(new AnswerOption
                {
                    Id = Guid.NewGuid(),
                    QuestionId = q.Id,
                    Text = new LocalizedText($"A{j}", $"A{j}", $"A{j}"),
                    IsCorrect = j == 0,
                    SortOrder = j,
                    CreatedAt = _dateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();
        return (template.Id, category.Id);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsCorrectQuestionCount()
    {
        using var db = TestDbContextFactory.Create();
        var (templateId, _) = await SeedTemplateWithQuestions(db, 10);

        var handler = CreateHandler(db);
        var result = await handler.Handle(
            new StartExamCommand(templateId, LicenseCategory.AB), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.TotalQuestions.Should().Be(10);
        result.Data.Questions.Should().HaveCount(10);
        result.Data.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_QuestionsAreShuffled_DifferentOrderOnMultipleCalls()
    {
        using var db = TestDbContextFactory.Create();
        var (templateId, _) = await SeedTemplateWithQuestions(db, 5);

        // Raise daily limit so we can run multiple exams
        await _cache.SetAsync("avtolider:settings:free_daily_exam_limit", "10");

        var handler = CreateHandler(db);

        // Run multiple times and check at least one different order
        var orders = new List<List<Guid>>();
        for (var i = 0; i < 5; i++)
        {
            // Reset sessions to allow new exam
            var sessions = db.ExamSessions.Where(s => s.Status == ExamStatus.InProgress).ToList();
            foreach (var s in sessions) s.Status = ExamStatus.Completed;
            await db.SaveChangesAsync();

            var result = await handler.Handle(
                new StartExamCommand(templateId, LicenseCategory.AB), CancellationToken.None);
            result.Success.Should().BeTrue($"Iteration {i} failed: {result.Error?.Code} - {result.Error?.Message}");
            orders.Add(result.Data!.Questions.Select(q => q.QuestionId).ToList());
        }

        // At least two runs should produce different orders (with 5 questions, probability of all same is 1/120)
        orders.Distinct(new ListComparer()).Count().Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task Handle_ExcludesInactiveQuestions()
    {
        using var db = TestDbContextFactory.Create();
        var (templateId, categoryId) = await SeedTemplateWithQuestions(db, 5);

        // Deactivate all but 3
        var questions = db.Questions.ToList();
        for (var i = 3; i < questions.Count; i++)
            questions[i].IsActive = false;
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(
            new StartExamCommand(templateId, LicenseCategory.AB), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.TotalQuestions.Should().BeLessOrEqualTo(3);
    }

    [Fact]
    public async Task Handle_ReadsFreeDailyLimitFromSettings()
    {
        using var db = TestDbContextFactory.Create();
        var (templateId, _) = await SeedTemplateWithQuestions(db, 5);

        // Set daily limit to 1 via cache
        await _cache.SetAsync("avtolider:settings:free_daily_exam_limit", "1");

        // First exam
        var handler = CreateHandler(db);
        var result1 = await handler.Handle(
            new StartExamCommand(templateId, LicenseCategory.AB), CancellationToken.None);
        result1.Success.Should().BeTrue();

        // Complete it
        var session = db.ExamSessions.First();
        session.Status = ExamStatus.Completed;
        await db.SaveChangesAsync();

        // Second exam should fail (limit = 1)
        var result2 = await handler.Handle(
            new StartExamCommand(templateId, LicenseCategory.AB), CancellationToken.None);
        result2.Success.Should().BeFalse();
        result2.Error!.Code.Should().Be("DAILY_LIMIT_REACHED");
    }

    [Fact]
    public async Task Handle_Unauthenticated_Fails()
    {
        using var db = TestDbContextFactory.Create();
        _currentUser.UserId = null;

        var handler = CreateHandler(db);
        var result = await handler.Handle(
            new StartExamCommand(Guid.NewGuid(), LicenseCategory.AB), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_ActiveSessionExists_Fails()
    {
        using var db = TestDbContextFactory.Create();
        var (templateId, _) = await SeedTemplateWithQuestions(db, 5);

        var handler = CreateHandler(db);
        var result1 = await handler.Handle(
            new StartExamCommand(templateId, LicenseCategory.AB), CancellationToken.None);
        result1.Success.Should().BeTrue();

        // Try starting another without completing first
        var result2 = await handler.Handle(
            new StartExamCommand(templateId, LicenseCategory.AB), CancellationToken.None);
        result2.Success.Should().BeFalse();
        result2.Error!.Code.Should().Be("ACTIVE_SESSION_EXISTS");
    }

    private class ListComparer : IEqualityComparer<List<Guid>>
    {
        public bool Equals(List<Guid>? x, List<Guid>? y) => x is not null && y is not null && x.SequenceEqual(y);
        public int GetHashCode(List<Guid> obj) => obj.Aggregate(0, (h, g) => h ^ g.GetHashCode());
    }
}
