using AutoTest.Application.Common.Interfaces;
using AutoTest.Application.Common.Models;
using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;
using AutoTest.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Features.Admin;

// DTOs
public record ExamTemplateDto(
    Guid Id, string TitleUz, string TitleUzLatin, string TitleRu,
    int TotalQuestions, int PassingScore, int TimeLimitMinutes, bool IsActive,
    List<PoolRuleDto> PoolRules);

public record PoolRuleDto(Guid Id, Guid CategoryId, string? CategoryName, Difficulty? Difficulty, int QuestionCount);

// GET templates (admin)
public record GetExamTemplatesQuery : IRequest<ApiResponse<List<ExamTemplateDto>>>;

public class GetExamTemplatesQueryHandler(
    IApplicationDbContext db) : IRequestHandler<GetExamTemplatesQuery, ApiResponse<List<ExamTemplateDto>>>
{
    public async Task<ApiResponse<List<ExamTemplateDto>>> Handle(GetExamTemplatesQuery request, CancellationToken ct)
    {
        var templates = await db.ExamTemplates
            .AsNoTracking()
            .Include(t => t.PoolRules)
                .ThenInclude(r => r.Category)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var dtos = templates.Select(t => new ExamTemplateDto(
            t.Id, t.Title.Uz, t.Title.UzLatin, t.Title.Ru,
            t.TotalQuestions, t.PassingScore, t.TimeLimitMinutes, t.IsActive,
            t.PoolRules.Select(r => new PoolRuleDto(
                r.Id, r.CategoryId, r.Category.Name.UzLatin, r.Difficulty, r.QuestionCount)).ToList()
        )).ToList();

        return ApiResponse<List<ExamTemplateDto>>.Ok(dtos);
    }
}

// CREATE template
public record CreateExamTemplateCommand(
    string TitleUz, string TitleUzLatin, string TitleRu,
    int TotalQuestions, int PassingScore, int TimeLimitMinutes, bool IsActive,
    List<CreatePoolRuleDto> PoolRules) : IRequest<ApiResponse<ExamTemplateDto>>;

public record CreatePoolRuleDto(Guid CategoryId, Difficulty? Difficulty, int QuestionCount);

public class CreateExamTemplateCommandValidator : AbstractValidator<CreateExamTemplateCommand>
{
    public CreateExamTemplateCommandValidator()
    {
        RuleFor(x => x.TitleUz).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleUzLatin).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TitleRu).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TotalQuestions).GreaterThan(0);
        RuleFor(x => x.PassingScore).InclusiveBetween(1, 100);
        RuleFor(x => x.TimeLimitMinutes).GreaterThan(0);
        RuleFor(x => x.PoolRules).NotEmpty();
        RuleForEach(x => x.PoolRules).ChildRules(r =>
        {
            r.RuleFor(p => p.CategoryId).NotEmpty();
            r.RuleFor(p => p.QuestionCount).GreaterThan(0);
        });
        // Pool rules sum must equal total questions
        RuleFor(x => x).Must(x => x.PoolRules.Sum(r => r.QuestionCount) == x.TotalQuestions)
            .WithMessage("Pool rules question count sum must equal TotalQuestions.");
    }
}

public class CreateExamTemplateCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<CreateExamTemplateCommandHandler> logger) : IRequestHandler<CreateExamTemplateCommand, ApiResponse<ExamTemplateDto>>
{
    public async Task<ApiResponse<ExamTemplateDto>> Handle(CreateExamTemplateCommand request, CancellationToken ct)
    {
        var now = dateTime.UtcNow;
        var template = new ExamTemplate
        {
            Id = Guid.NewGuid(),
            Title = new LocalizedText(request.TitleUz, request.TitleUzLatin, request.TitleRu),
            TotalQuestions = request.TotalQuestions,
            PassingScore = request.PassingScore,
            TimeLimitMinutes = request.TimeLimitMinutes,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        var rules = request.PoolRules.Select(r => new ExamPoolRule
        {
            Id = Guid.NewGuid(),
            ExamTemplateId = template.Id,
            CategoryId = r.CategoryId,
            Difficulty = r.Difficulty,
            QuestionCount = r.QuestionCount,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        template.PoolRules = rules;
        db.ExamTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("ExamTemplate created: {TemplateId}", template.Id);

        return ApiResponse<ExamTemplateDto>.Ok(new ExamTemplateDto(
            template.Id, request.TitleUz, request.TitleUzLatin, request.TitleRu,
            template.TotalQuestions, template.PassingScore, template.TimeLimitMinutes, template.IsActive,
            rules.Select(r => new PoolRuleDto(r.Id, r.CategoryId, null, r.Difficulty, r.QuestionCount)).ToList()));
    }
}

// UPDATE template
public record UpdateExamTemplateCommand(
    Guid Id,
    string TitleUz, string TitleUzLatin, string TitleRu,
    int TotalQuestions, int PassingScore, int TimeLimitMinutes, bool IsActive,
    List<CreatePoolRuleDto> PoolRules) : IRequest<ApiResponse>;

public class UpdateExamTemplateCommandValidator : AbstractValidator<UpdateExamTemplateCommand>
{
    public UpdateExamTemplateCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TitleUz).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TotalQuestions).GreaterThan(0);
        RuleFor(x => x.PassingScore).InclusiveBetween(1, 100);
        RuleFor(x => x.TimeLimitMinutes).GreaterThan(0);
        RuleFor(x => x.PoolRules).NotEmpty();
        RuleFor(x => x).Must(x => x.PoolRules.Sum(r => r.QuestionCount) == x.TotalQuestions)
            .WithMessage("Pool rules question count sum must equal TotalQuestions.");
    }
}

public class UpdateExamTemplateCommandHandler(
    IApplicationDbContext db,
    IDateTimeProvider dateTime,
    ILogger<UpdateExamTemplateCommandHandler> logger) : IRequestHandler<UpdateExamTemplateCommand, ApiResponse>
{
    public async Task<ApiResponse> Handle(UpdateExamTemplateCommand request, CancellationToken ct)
    {
        var template = await db.ExamTemplates
            .Include(t => t.PoolRules)
            .FirstOrDefaultAsync(t => t.Id == request.Id, ct);

        if (template is null)
            return ApiResponse.Fail("TEMPLATE_NOT_FOUND", "Exam template not found.");

        var now = dateTime.UtcNow;
        template.Title = new LocalizedText(request.TitleUz, request.TitleUzLatin, request.TitleRu);
        template.TotalQuestions = request.TotalQuestions;
        template.PassingScore = request.PassingScore;
        template.TimeLimitMinutes = request.TimeLimitMinutes;
        template.IsActive = request.IsActive;
        template.UpdatedAt = now;

        // Replace pool rules: remove old, add new
        db.ExamPoolRules.RemoveRange(template.PoolRules);
        var newRules = request.PoolRules.Select(r => new ExamPoolRule
        {
            Id = Guid.NewGuid(),
            ExamTemplateId = template.Id,
            CategoryId = r.CategoryId,
            Difficulty = r.Difficulty,
            QuestionCount = r.QuestionCount,
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        template.PoolRules = newRules;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("ExamTemplate updated: {TemplateId}", template.Id);
        return ApiResponse.Ok();
    }
}
