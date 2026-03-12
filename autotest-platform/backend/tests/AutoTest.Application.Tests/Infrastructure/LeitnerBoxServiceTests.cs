using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Tests.TestHelpers;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using AutoTest.Infrastructure.Services;
using FluentAssertions;

namespace AutoTest.Application.Tests.Infrastructure;

public class LeitnerBoxServiceTests
{
    private static readonly int[] LeitnerIntervals = [1, 2, 4, 8, 16];
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };

    [Fact]
    public async Task PromoteLeitnerBox_AdvancesFromBox1ToBox2()
    {
        using var db = TestDbContextFactory.Create();
        var (userId, questionId) = SeedUserAndQuestion(db);
        await db.SaveChangesAsync();

        var service = new LeitnerBoxService(db, _dateTime);
        await service.PromoteLeitnerBoxAsync(userId, questionId);

        var state = db.UserQuestionStates.First(s => s.UserId == userId && s.QuestionId == questionId);
        state.LeitnerBox.Should().Be(LeitnerBox.Box2);
        state.CorrectAttempts.Should().Be(1);
        state.TotalAttempts.Should().Be(1);
        state.NextReviewDate.Should().Be(_dateTime.UtcNow.AddDays(LeitnerIntervals[1])); // Box2 = 2 days
    }

    [Fact]
    public async Task PromoteLeitnerBox_CapsAtBox5()
    {
        using var db = TestDbContextFactory.Create();
        var (userId, questionId) = SeedUserAndQuestion(db);

        // Pre-create state at Box5
        db.UserQuestionStates.Add(new UserQuestionState
        {
            UserId = userId,
            QuestionId = questionId,
            LeitnerBox = LeitnerBox.Box5,
            NextReviewDate = _dateTime.UtcNow,
            TotalAttempts = 10,
            CorrectAttempts = 10
        });
        await db.SaveChangesAsync();

        var service = new LeitnerBoxService(db, _dateTime);
        await service.PromoteLeitnerBoxAsync(userId, questionId);

        var state = db.UserQuestionStates.First(s => s.UserId == userId && s.QuestionId == questionId);
        state.LeitnerBox.Should().Be(LeitnerBox.Box5); // stays at 5
        state.NextReviewDate.Should().Be(_dateTime.UtcNow.AddDays(LeitnerIntervals[4])); // 16 days
    }

    [Fact]
    public async Task DemoteLeitnerBox_ResetsToBox1()
    {
        using var db = TestDbContextFactory.Create();
        var (userId, questionId) = SeedUserAndQuestion(db);

        // Pre-create state at Box4
        db.UserQuestionStates.Add(new UserQuestionState
        {
            UserId = userId,
            QuestionId = questionId,
            LeitnerBox = LeitnerBox.Box4,
            NextReviewDate = _dateTime.UtcNow,
            TotalAttempts = 5,
            CorrectAttempts = 4
        });
        await db.SaveChangesAsync();

        var service = new LeitnerBoxService(db, _dateTime);
        await service.DemoteLeitnerBoxAsync(userId, questionId);

        var state = db.UserQuestionStates.First(s => s.UserId == userId && s.QuestionId == questionId);
        state.LeitnerBox.Should().Be(LeitnerBox.Box1);
        state.NextReviewDate.Should().Be(_dateTime.UtcNow.AddDays(LeitnerIntervals[0])); // 1 day
        state.TotalAttempts.Should().Be(6);
    }

    [Fact]
    public async Task ReviewIntervals_AreCorrect()
    {
        using var db = TestDbContextFactory.Create();
        var (userId, questionId) = SeedUserAndQuestion(db);
        await db.SaveChangesAsync();

        var service = new LeitnerBoxService(db, _dateTime);

        // Promote through all boxes
        for (var box = 1; box <= 5; box++)
        {
            await service.PromoteLeitnerBoxAsync(userId, questionId);
            var state = db.UserQuestionStates.First(s => s.UserId == userId && s.QuestionId == questionId);
            var expectedBox = box < 5 ? box + 1 : 5;
            state.LeitnerBox.Should().Be((LeitnerBox)expectedBox);
            state.NextReviewDate.Should().Be(_dateTime.UtcNow.AddDays(LeitnerIntervals[expectedBox - 1]));
        }
    }

    [Fact]
    public async Task RecordAnswer_CorrectPromotes()
    {
        using var db = TestDbContextFactory.Create();
        var (userId, questionId) = SeedUserAndQuestion(db);
        await db.SaveChangesAsync();

        var service = new LeitnerBoxService(db, _dateTime);
        await service.RecordAnswerAsync(userId, questionId, true);

        var state = db.UserQuestionStates.First(s => s.UserId == userId && s.QuestionId == questionId);
        state.LeitnerBox.Should().Be(LeitnerBox.Box2);
    }

    [Fact]
    public async Task RecordAnswer_IncorrectDemotes()
    {
        using var db = TestDbContextFactory.Create();
        var (userId, questionId) = SeedUserAndQuestion(db);

        db.UserQuestionStates.Add(new UserQuestionState
        {
            UserId = userId,
            QuestionId = questionId,
            LeitnerBox = LeitnerBox.Box3,
            NextReviewDate = _dateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new LeitnerBoxService(db, _dateTime);
        await service.RecordAnswerAsync(userId, questionId, false);

        var state = db.UserQuestionStates.First(s => s.UserId == userId && s.QuestionId == questionId);
        state.LeitnerBox.Should().Be(LeitnerBox.Box1);
    }

    private (Guid userId, Guid questionId) SeedUserAndQuestion(IApplicationDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = "+998901234567",
            Role = UserRole.User,
            AuthProvider = AuthProvider.Phone,
            CreatedAt = _dateTime.UtcNow
        };
        db.Users.Add(user);

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

        var question = new Question
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Text = new LocalizedText("Q", "Q", "Q"),
            Explanation = new LocalizedText("E", "E", "E"),
            Difficulty = Difficulty.Easy,
            TicketNumber = 1,
            LicenseCategory = LicenseCategory.AB,
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.Questions.Add(question);

        return (user.Id, question.Id);
    }
}
