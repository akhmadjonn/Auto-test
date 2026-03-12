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

public class StartMarathonCommandTests
{
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };
    private readonly FakeCurrentUser _currentUser = new() { UserId = Guid.NewGuid() };
    private readonly FakeFileStorageService _storage = new();
    private readonly ILogger<StartMarathonCommandHandler> _logger = Substitute.For<ILogger<StartMarathonCommandHandler>>();

    private StartMarathonCommandHandler CreateHandler(IApplicationDbContext db) =>
        new(db, _currentUser, _storage, _dateTime, _logger);

    [Fact]
    public async Task Handle_LoadsAllQuestions_NoTimer()
    {
        using var db = TestDbContextFactory.Create();
        SeedQuestions(db, 30);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new StartMarathonCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.TotalQuestions.Should().Be(30);
        // First batch is limited to 20
        result.Data.Questions.Should().HaveCountLessOrEqualTo(20);
    }

    [Fact]
    public async Task Handle_ResumesExistingMarathon()
    {
        using var db = TestDbContextFactory.Create();
        SeedQuestions(db, 10);
        await db.SaveChangesAsync();

        // Create an existing marathon session
        var existingSession = new ExamSession
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId!.Value,
            Status = ExamStatus.InProgress,
            Mode = ExamMode.Marathon,
            LicenseCategory = LicenseCategory.AB,
            ExpiresAt = null,
            CreatedAt = _dateTime.UtcNow
        };

        var answerOptionId = Guid.NewGuid(); // dummy answer for "answered" questions
        var questions = db.Questions.ToList();
        var sessionQuestions = new List<SessionQuestion>();
        for (var i = 0; i < questions.Count; i++)
        {
            sessionQuestions.Add(new SessionQuestion
            {
                Id = Guid.NewGuid(),
                ExamSessionId = existingSession.Id,
                QuestionId = questions[i].Id,
                Order = i + 1,
                SelectedAnswerId = i < 3 ? db.AnswerOptions.First(a => a.QuestionId == questions[i].Id).Id : null,
                CreatedAt = _dateTime.UtcNow
            });
        }

        existingSession.SessionQuestions = sessionQuestions;
        db.ExamSessions.Add(existingSession);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new StartMarathonCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.SessionId.Should().Be(existingSession.Id);
        result.Data.LastQuestionIndex.Should().Be(3);
    }

    [Fact]
    public async Task Handle_NoQuestions_ReturnsFail()
    {
        using var db = TestDbContextFactory.Create();
        // No questions seeded

        var handler = CreateHandler(db);
        var result = await handler.Handle(new StartMarathonCommand(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("NO_QUESTIONS");
    }

    private void SeedQuestions(IApplicationDbContext db, int count)
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

        for (var i = 0; i < count; i++)
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

            db.AnswerOptions.Add(new AnswerOption
            {
                Id = Guid.NewGuid(),
                QuestionId = q.Id,
                Text = new LocalizedText("A", "A", "A"),
                IsCorrect = true,
                SortOrder = 0,
                CreatedAt = _dateTime.UtcNow
            });
        }
    }
}
