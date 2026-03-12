using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Features.Exams;
using AutoTest.Application.Features.Questions;
using AutoTest.Application.Tests.TestHelpers;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AutoTest.Application.Tests.Integration;

/// <summary>
/// Integration test: Create question with image → Read with presigned URL → Deactivate → Verify excluded from exam.
/// Tests the full admin question lifecycle across multiple handlers sharing the same DbContext.
/// </summary>
public class AdminQuestionCrudIntegrationTests
{
    private readonly FakeDateTimeProvider _dateTime = new() { UtcNow = DateTimeOffset.UtcNow };
    private readonly FakeCurrentUser _currentUser = new() { UserId = Guid.NewGuid(), IsAdmin = true };
    private readonly FakeFileStorageService _storage = new();
    private readonly FakeCacheService _cache = new();
    private readonly IImageProcessingService _imageProcessor = Substitute.For<IImageProcessingService>();

    [Fact]
    public async Task FullCycle_CreateWithImage_Read_Deactivate_ExcludedFromExam()
    {
        using var db = TestDbContextFactory.Create();

        // Seed a category for questions
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("PDD", "PDD", "ПДД"),
            Description = new LocalizedText("Desc", "Desc", "Desc"),
            Slug = "pdd",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        // --- Step 1: Create question with image ---
        var fakeImage = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // fake PNG header
        var processedImage = new MemoryStream([1, 2, 3]);
        var thumbnail = new MemoryStream([4, 5, 6]);

        _imageProcessor.ProcessImageAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ImageProcessingResult(processedImage, thumbnail, "image/webp"));

        var createHandler = new CreateQuestionCommandHandler(
            db, _imageProcessor, _storage, _dateTime,
            Substitute.For<ILogger<CreateQuestionCommandHandler>>());

        var createCmd = new CreateQuestionCommand(
            CategoryId: category.Id,
            TextUz: "Qizil chiroqda nima qilasiz?",
            TextUzLatin: "Qizil chiroqda nima qilasiz?",
            TextRu: "Что делать на красный свет?",
            ExplanationUz: "To'xtash kerak",
            ExplanationUzLatin: "To'xtash kerak",
            ExplanationRu: "Нужно остановиться",
            Difficulty: Difficulty.Easy,
            TicketNumber: 1,
            LicenseCategory: LicenseCategory.AB,
            IsActive: true,
            QuestionImage: fakeImage,
            QuestionImageFileName: "signal.png",
            AnswerOptions:
            [
                new("To'xtash", "To'xtash", "Остановиться", true, null, null),
                new("O'tish", "O'tish", "Проехать", false, null, null),
                new("Tezlashtirish", "Tezlashtirish", "Ускориться", false, null, null)
            ]);

        var createResult = await createHandler.Handle(createCmd, CancellationToken.None);

        createResult.Success.Should().BeTrue();
        var questionId = createResult.Data;

        // Verify question in DB
        var savedQuestion = await db.Questions
            .Include(q => q.AnswerOptions)
            .FirstOrDefaultAsync(q => q.Id == questionId);
        savedQuestion.Should().NotBeNull();
        savedQuestion!.ImageUrl.Should().NotBeNullOrEmpty();
        savedQuestion.AnswerOptions.Should().HaveCount(3);
        savedQuestion.AnswerOptions.Count(a => a.IsCorrect).Should().Be(1);
        savedQuestion.IsActive.Should().BeTrue();

        // --- Step 2: Read question via GetQuestionsByCategoryQuery (presigned URL) ---
        var readHandler = new GetQuestionsByCategoryQueryHandler(db, _storage);
        var readResult = await readHandler.Handle(
            new GetQuestionsByCategoryQuery(category.Id, Language.UzLatin), CancellationToken.None);

        readResult.Success.Should().BeTrue();
        readResult.Data!.Items.Should().HaveCount(1);

        var questionDto = readResult.Data.Items[0];
        questionDto.Id.Should().Be(questionId);
        questionDto.Text.Should().Be("Qizil chiroqda nima qilasiz?");
        questionDto.ImageUrl.Should().Contain("signed=1"); // presigned URL from FakeFileStorageService
        questionDto.AnswerOptions.Should().HaveCount(3);

        // --- Step 3: Deactivate the question ---
        var toggleHandler = new ToggleQuestionStatusCommandHandler(
            db, _dateTime,
            Substitute.For<ILogger<ToggleQuestionStatusCommandHandler>>());

        var toggleResult = await toggleHandler.Handle(
            new ToggleQuestionStatusCommand(questionId, false), CancellationToken.None);

        toggleResult.Success.Should().BeTrue();

        // Verify deactivated in DB
        var deactivated = await db.Questions.FindAsync(questionId);
        deactivated!.IsActive.Should().BeFalse();

        // --- Step 4: Verify question is excluded from category listing (active-only query) ---
        var readAfterResult = await readHandler.Handle(
            new GetQuestionsByCategoryQuery(category.Id, Language.UzLatin), CancellationToken.None);

        readAfterResult.Success.Should().BeTrue();
        readAfterResult.Data!.Items.Should().BeEmpty("deactivated question should not appear in active-only listing");

        // --- Step 5: Verify excluded from exam pool ---
        // Seed exam template with pool rule pointing to same category
        var template = new ExamTemplate
        {
            Id = Guid.NewGuid(),
            Title = new LocalizedText("Test", "Test", "Test"),
            TotalQuestions = 1,
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
            QuestionCount = 1,
            CreatedAt = _dateTime.UtcNow
        };
        db.ExamPoolRules.Add(poolRule);
        await db.SaveChangesAsync();

        var examHandler = new StartExamCommandHandler(
            db, _currentUser, _storage, _cache, new FakeDistributedLockService(), _dateTime,
            Substitute.For<ILogger<StartExamCommandHandler>>());

        var examResult = await examHandler.Handle(
            new StartExamCommand(template.Id, LicenseCategory.AB), CancellationToken.None);

        // No active questions in the category → exam should fail or return 0 questions
        examResult.Success.Should().BeFalse();
        examResult.Error!.Code.Should().Be("NO_QUESTIONS");
    }

    [Fact]
    public async Task CreateQuestion_InvalidCategory_Fails()
    {
        using var db = TestDbContextFactory.Create();

        _imageProcessor.ProcessImageAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ImageProcessingResult(new MemoryStream(), new MemoryStream(), "image/webp"));

        var handler = new CreateQuestionCommandHandler(
            db, _imageProcessor, _storage, _dateTime,
            Substitute.For<ILogger<CreateQuestionCommandHandler>>());

        var cmd = new CreateQuestionCommand(
            CategoryId: Guid.NewGuid(), // non-existent
            TextUz: "Q", TextUzLatin: "Q", TextRu: "Q",
            ExplanationUz: "E", ExplanationUzLatin: "E", ExplanationRu: "E",
            Difficulty: Difficulty.Easy, TicketNumber: 1, LicenseCategory: LicenseCategory.AB, IsActive: true,
            QuestionImage: null, QuestionImageFileName: null,
            AnswerOptions:
            [
                new("A1", "A1", "A1", true, null, null),
                new("A2", "A2", "A2", false, null, null)
            ]);

        var result = await handler.Handle(cmd, CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Error!.Code.Should().Be("CATEGORY_NOT_FOUND");
    }

    [Fact]
    public async Task CreateQuestion_NoImage_StillWorks()
    {
        using var db = TestDbContextFactory.Create();

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("Cat", "Cat", "Cat"),
            Description = new LocalizedText("D", "D", "D"),
            Slug = $"cat-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var handler = new CreateQuestionCommandHandler(
            db, _imageProcessor, _storage, _dateTime,
            Substitute.For<ILogger<CreateQuestionCommandHandler>>());

        var cmd = new CreateQuestionCommand(
            CategoryId: category.Id,
            TextUz: "Savol", TextUzLatin: "Savol", TextRu: "Вопрос",
            ExplanationUz: "Izoh", ExplanationUzLatin: "Izoh", ExplanationRu: "Объяснение",
            Difficulty: Difficulty.Medium, TicketNumber: 5, LicenseCategory: LicenseCategory.CD, IsActive: true,
            QuestionImage: null, QuestionImageFileName: null,
            AnswerOptions:
            [
                new("Ha", "Ha", "Да", true, null, null),
                new("Yo'q", "Yo'q", "Нет", false, null, null)
            ]);

        var result = await handler.Handle(cmd, CancellationToken.None);
        result.Success.Should().BeTrue();

        var saved = await db.Questions.Include(q => q.AnswerOptions).FirstAsync(q => q.Id == result.Data);
        saved.ImageUrl.Should().BeNull();
        saved.ThumbnailUrl.Should().BeNull();
        saved.AnswerOptions.Should().HaveCount(2);
        saved.Text.Get(Language.Ru).Should().Be("Вопрос");
    }

    [Fact]
    public async Task ToggleStatus_Reactivate_AppearsInExam()
    {
        using var db = TestDbContextFactory.Create();

        // Seed category + 1 question (inactive)
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = new LocalizedText("Cat", "Cat", "Cat"),
            Description = new LocalizedText("D", "D", "D"),
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
            IsActive = false, // initially inactive
            CreatedAt = _dateTime.UtcNow
        };
        db.Questions.Add(question);

        db.AnswerOptions.Add(new AnswerOption
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Text = new LocalizedText("A", "A", "A"),
            IsCorrect = true,
            SortOrder = 0,
            CreatedAt = _dateTime.UtcNow
        });

        // Seed exam template
        var template = new ExamTemplate
        {
            Id = Guid.NewGuid(),
            Title = new LocalizedText("T", "T", "T"),
            TotalQuestions = 1,
            PassingScore = 80,
            TimeLimitMinutes = 10,
            IsActive = true,
            CreatedAt = _dateTime.UtcNow
        };
        db.ExamTemplates.Add(template);

        db.ExamPoolRules.Add(new ExamPoolRule
        {
            Id = Guid.NewGuid(),
            ExamTemplateId = template.Id,
            CategoryId = category.Id,
            QuestionCount = 1,
            CreatedAt = _dateTime.UtcNow
        });

        await db.SaveChangesAsync();

        // Exam should fail — no active questions
        var examHandler = new StartExamCommandHandler(
            db, _currentUser, _storage, _cache, new FakeDistributedLockService(), _dateTime,
            Substitute.For<ILogger<StartExamCommandHandler>>());

        var examResult1 = await examHandler.Handle(
            new StartExamCommand(template.Id, LicenseCategory.AB), CancellationToken.None);
        examResult1.Success.Should().BeFalse();
        examResult1.Error!.Code.Should().Be("NO_QUESTIONS");

        // Reactivate question
        var toggleHandler = new ToggleQuestionStatusCommandHandler(
            db, _dateTime,
            Substitute.For<ILogger<ToggleQuestionStatusCommandHandler>>());

        var toggleResult = await toggleHandler.Handle(
            new ToggleQuestionStatusCommand(question.Id, true), CancellationToken.None);
        toggleResult.Success.Should().BeTrue();

        // Now exam should succeed
        var examResult2 = await examHandler.Handle(
            new StartExamCommand(template.Id, LicenseCategory.AB), CancellationToken.None);
        examResult2.Success.Should().BeTrue();
        examResult2.Data!.Questions.Should().HaveCount(1);
        examResult2.Data.Questions[0].QuestionId.Should().Be(question.Id);
    }
}
