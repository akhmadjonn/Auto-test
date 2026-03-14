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

public class CompleteExamCommandTests
{
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };
    private readonly FakeCurrentUser _currentUser = new() { UserId = Guid.NewGuid() };
    private readonly FakeFileStorageService _storage = new();
    private readonly ILogger<CompleteExamCommandHandler> _logger = Substitute.For<ILogger<CompleteExamCommandHandler>>();

    private readonly FakeCacheService _cache = new();

    private CompleteExamCommandHandler CreateHandler(IApplicationDbContext db) =>
        new(db, _currentUser, _storage, _dateTime, _cache, _logger);

    [Fact]
    public async Task Handle_AllCorrect_Returns100PercentScore()
    {
        using var db = TestDbContextFactory.Create();
        var template = SeedTemplate(db);
        var session = SeedSession(db, template.Id, _currentUser.UserId!.Value, 5);
        // Mark all answers correct
        foreach (var sq in session.SessionQuestions)
            sq.IsCorrect = true;
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new CompleteExamCommand(session.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.CorrectAnswers.Should().Be(5);
        result.Data.Score.Should().Be(100);
        result.Data.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoneCorrect_Returns0Score()
    {
        using var db = TestDbContextFactory.Create();
        var template = SeedTemplate(db);
        var session = SeedSession(db, template.Id, _currentUser.UserId!.Value, 5);
        // Mark all answers wrong
        foreach (var sq in session.SessionQuestions)
            sq.IsCorrect = false;
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new CompleteExamCommand(session.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.CorrectAnswers.Should().Be(0);
        result.Data.Score.Should().Be(0);
        result.Data.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PartialCorrect_CalculatesScoreCorrectly()
    {
        using var db = TestDbContextFactory.Create();
        var template = SeedTemplate(db);
        var session = SeedSession(db, template.Id, _currentUser.UserId!.Value, 10);
        // 8/10 correct = 80% — exactly passing
        var questions = session.SessionQuestions.ToList();
        for (var i = 0; i < 10; i++)
            questions[i].IsCorrect = i < 8;
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new CompleteExamCommand(session.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Score.Should().Be(80);
        result.Data.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_ReturnsFail()
    {
        using var db = TestDbContextFactory.Create();
        var template = SeedTemplate(db);
        var session = SeedSession(db, template.Id, _currentUser.UserId!.Value, 5);
        session.Status = ExamStatus.Completed;
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new CompleteExamCommand(session.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("ALREADY_COMPLETED");
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsFail()
    {
        using var db = TestDbContextFactory.Create();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new CompleteExamCommand(Guid.NewGuid()), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UpdatesLeitnerStates()
    {
        using var db = TestDbContextFactory.Create();
        var template = SeedTemplate(db);
        var session = SeedSession(db, template.Id, _currentUser.UserId!.Value, 3);
        var questions = session.SessionQuestions.ToList();
        questions[0].IsCorrect = true;
        questions[1].IsCorrect = false;
        questions[2].IsCorrect = true;
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        await handler.Handle(new CompleteExamCommand(session.Id), CancellationToken.None);

        var states = db.UserQuestionStates.Where(s => s.UserId == _currentUser.UserId!.Value).ToList();
        states.Should().HaveCount(3);

        var correctState = states.First(s => s.QuestionId == questions[0].QuestionId);
        correctState.LeitnerBox.Should().Be(LeitnerBox.Box2); // advanced from 1 → 2
        correctState.CorrectAttempts.Should().Be(1);

        var wrongState = states.First(s => s.QuestionId == questions[1].QuestionId);
        wrongState.LeitnerBox.Should().Be(LeitnerBox.Box1); // reset to 1
    }

    private ExamTemplate SeedTemplate(IApplicationDbContext db)
    {
        var template = new ExamTemplate
        {
            Id = Guid.NewGuid(),
            Title = new LocalizedText("Test", "Test", "Test"),
            TotalQuestions = 20,
            PassingScore = 80,
            TimeLimitMinutes = 20,
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.ExamTemplates.Add(template);
        return template;
    }

    private ExamSession SeedSession(IApplicationDbContext db, Guid templateId, Guid userId, int questionCount)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("Cat", "Cat", "Cat"),
            Description = new LocalizedText("Cat", "Cat", "Cat"),
            Slug = $"cat-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.Categories.Add(category);

        var session = new ExamSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExamTemplateId = templateId,
            Status = ExamStatus.InProgress,
            Mode = ExamMode.Exam,
            LicenseCategory = LicenseCategory.AB,
            ExpiresAt = _dateTime.UtcNow.AddMinutes(20),
            CreatedAt = _dateTime.UtcNow
        };

        var sessionQuestions = new List<SessionQuestion>();
        for (var i = 0; i < questionCount; i++)
        {
            var q = new Question
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Text = new LocalizedText($"Q{i}", $"Q{i}", $"Q{i}"),
                Explanation = new LocalizedText("E", "E", "E"),
                Difficulty = Difficulty.Easy,
                TicketNumber = 1,
                LicenseCategory = LicenseCategory.AB,
                IsActive = true,
                CreatedAt = _dateTime.UtcNow
            };
            db.Questions.Add(q);

            var correctOption = new AnswerOption
            {
                Id = Guid.NewGuid(),
                QuestionId = q.Id,
                Text = new LocalizedText("Correct", "Correct", "Correct"),
                IsCorrect = true,
                SortOrder = 0,
                CreatedAt = _dateTime.UtcNow
            };
            db.AnswerOptions.Add(correctOption);

            sessionQuestions.Add(new SessionQuestion
            {
                Id = Guid.NewGuid(),
                ExamSessionId = session.Id,
                QuestionId = q.Id,
                SelectedAnswerId = correctOption.Id,
                Order = i + 1,
                CreatedAt = _dateTime.UtcNow
            });
        }

        session.SessionQuestions = sessionQuestions;
        db.ExamSessions.Add(session);
        db.SaveChangesAsync().GetAwaiter().GetResult();

        return session;
    }
}
