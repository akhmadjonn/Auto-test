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

public class StartTicketExamCommandTests
{
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };
    private readonly FakeCurrentUser _currentUser = new() { UserId = Guid.NewGuid() };
    private readonly FakeFileStorageService _storage = new();
    private readonly ILogger<StartTicketExamCommandHandler> _logger = Substitute.For<ILogger<StartTicketExamCommandHandler>>();

    private StartTicketExamCommandHandler CreateHandler(IApplicationDbContext db) =>
        new(db, _currentUser, _storage, _dateTime, _logger);

    [Fact]
    public async Task Handle_LoadsCorrectTicketQuestions()
    {
        using var db = TestDbContextFactory.Create();
        var category = SeedCategory(db);

        // Seed 5 questions for ticket 1, 3 for ticket 2
        SeedQuestions(db, category.Id, ticketNumber: 1, count: 5);
        SeedQuestions(db, category.Id, ticketNumber: 2, count: 3);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new StartTicketExamCommand(1), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.TotalQuestions.Should().Be(5);
        result.Data.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_TicketNotFound_ReturnsFail()
    {
        using var db = TestDbContextFactory.Create();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new StartTicketExamCommand(999), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_HasActiveSession_ReturnsFail()
    {
        using var db = TestDbContextFactory.Create();
        var category = SeedCategory(db);
        SeedQuestions(db, category.Id, ticketNumber: 1, count: 3);

        // Create active session
        db.ExamSessions.Add(new ExamSession
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId!.Value,
            Status = ExamStatus.InProgress,
            Mode = ExamMode.Exam,
            LicenseCategory = LicenseCategory.AB,
            CreatedAt = _dateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);
        var result = await handler.Handle(new StartTicketExamCommand(1), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("ACTIVE_SESSION_EXISTS");
    }

    [Fact]
    public void Validator_TicketNumber_MustBePositive()
    {
        var validator = new StartTicketExamCommandValidator();
        var result = validator.Validate(new StartTicketExamCommand(0));
        result.IsValid.Should().BeFalse();
    }

    private Category SeedCategory(IApplicationDbContext db)
    {
        var cat = new Category
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("Cat", "Cat", "Cat"),
            Description = new LocalizedText("Cat", "Cat", "Cat"),
            Slug = $"cat-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.Categories.Add(cat);
        return cat;
    }

    private void SeedQuestions(IApplicationDbContext db, Guid categoryId, int ticketNumber, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var q = new Question
            {
                Id = Guid.NewGuid(),
                CategoryId = categoryId,
                Text = new LocalizedText($"Q{i}", $"Q{i}", $"Q{i}"),
                Explanation = new LocalizedText("E", "E", "E"),
                Difficulty = Difficulty.Easy,
                TicketNumber = ticketNumber,
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
