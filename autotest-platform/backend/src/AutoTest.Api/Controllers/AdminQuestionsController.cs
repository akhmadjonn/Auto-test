using AutoTest.Application.Features.Questions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Api.Controllers;

[ApiController]
[Route("api/v1/admin/questions")]
[Authorize(Roles = "Admin")]
public class AdminQuestionsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CreateQuestionFormModel form, CancellationToken ct)
    {
        var optionImages = new List<Stream?>();
        var optionImageNames = new List<string?>();

        for (var i = 0; i < (form.AnswerOptions?.Count ?? 0); i++)
        {
            var imgFile = form.AnswerOptionImages?.Count > i ? form.AnswerOptionImages[i] : null;
            optionImages.Add(imgFile?.OpenReadStream());
            optionImageNames.Add(imgFile?.FileName);
        }

        var command = new CreateQuestionCommand(
            form.CategoryId,
            form.TextUz, form.TextUzLatin, form.TextRu,
            form.ExplanationUz, form.ExplanationUzLatin, form.ExplanationRu,
            form.Difficulty, form.TicketNumber, form.LicenseCategory,
            form.IsActive,
            form.QuestionImage?.OpenReadStream(),
            form.QuestionImage?.FileName,
            form.AnswerOptions?.Select((o, i) => new CreateAnswerOptionDto(
                o.TextUz, o.TextUzLatin, o.TextRu, o.IsCorrect,
                optionImages[i], optionImageNames[i])).ToList() ?? []);

        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(Guid id, [FromForm] UpdateQuestionFormModel form, CancellationToken ct)
    {
        var optionImages = new List<Stream?>();
        var optionImageNames = new List<string?>();

        for (var i = 0; i < (form.AnswerOptions?.Count ?? 0); i++)
        {
            var imgFile = form.AnswerOptionImages?.Count > i ? form.AnswerOptionImages[i] : null;
            optionImages.Add(imgFile?.OpenReadStream());
            optionImageNames.Add(imgFile?.FileName);
        }

        var command = new UpdateQuestionCommand(
            id,
            form.TextUz, form.TextUzLatin, form.TextRu,
            form.ExplanationUz, form.ExplanationUzLatin, form.ExplanationRu,
            form.Difficulty, form.TicketNumber, form.LicenseCategory, form.IsActive,
            form.RemoveQuestionImage,
            form.NewQuestionImage?.OpenReadStream(),
            form.NewQuestionImage?.FileName,
            form.AnswerOptions?.Select((o, i) => new UpdateAnswerOptionDto(
                o.ExistingId, o.TextUz, o.TextUzLatin, o.TextRu, o.IsCorrect,
                o.RemoveImage, optionImages[i], optionImageNames[i])).ToList() ?? []);

        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ToggleStatus(Guid id, [FromBody] ToggleStatusRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ToggleQuestionStatusCommand(id, req.IsActive), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPatch("bulk-status")]
    public async Task<IActionResult> BulkToggleStatus([FromBody] BulkToggleStatusCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteQuestionCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpDelete("{id}/permanent")]
    public async Task<IActionResult> PermanentDelete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new PermanentDeleteQuestionCommand(id), ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> BulkImport(IFormFile excel, IFormFile? images, CancellationToken ct)
    {
        var command = new BulkImportQuestionsCommand(
            excel.OpenReadStream(),
            images?.OpenReadStream());
        var result = await mediator.Send(command, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("export-template")]
    public async Task<IActionResult> ExportTemplate(CancellationToken ct)
    {
        var result = await mediator.Send(new ExportExcelTemplateQuery(), ct);
        return File(result.Data!, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "questions-template.xlsx");
    }
}

// Form models for multipart
public class CreateQuestionFormModel
{
    public Guid CategoryId { get; set; }
    public string TextUz { get; set; } = null!;
    public string TextUzLatin { get; set; } = null!;
    public string TextRu { get; set; } = null!;
    public string ExplanationUz { get; set; } = null!;
    public string ExplanationUzLatin { get; set; } = null!;
    public string ExplanationRu { get; set; } = null!;
    public AutoTest.Domain.Common.Enums.Difficulty Difficulty { get; set; }
    public int TicketNumber { get; set; }
    public AutoTest.Domain.Common.Enums.LicenseCategory LicenseCategory { get; set; }
    public bool IsActive { get; set; }
    public IFormFile? QuestionImage { get; set; }
    public List<IFormFile>? AnswerOptionImages { get; set; }
    public List<AnswerOptionFormItem>? AnswerOptions { get; set; }
}

public class UpdateQuestionFormModel
{
    public string TextUz { get; set; } = null!;
    public string TextUzLatin { get; set; } = null!;
    public string TextRu { get; set; } = null!;
    public string ExplanationUz { get; set; } = null!;
    public string ExplanationUzLatin { get; set; } = null!;
    public string ExplanationRu { get; set; } = null!;
    public AutoTest.Domain.Common.Enums.Difficulty Difficulty { get; set; }
    public int TicketNumber { get; set; }
    public AutoTest.Domain.Common.Enums.LicenseCategory LicenseCategory { get; set; }
    public bool IsActive { get; set; }
    public bool RemoveQuestionImage { get; set; }
    public IFormFile? NewQuestionImage { get; set; }
    public List<IFormFile>? AnswerOptionImages { get; set; }
    public List<UpdateAnswerOptionFormItem>? AnswerOptions { get; set; }
}

public class AnswerOptionFormItem
{
    public string TextUz { get; set; } = null!;
    public string TextUzLatin { get; set; } = null!;
    public string TextRu { get; set; } = null!;
    public bool IsCorrect { get; set; }
}

public class UpdateAnswerOptionFormItem : AnswerOptionFormItem
{
    public Guid? ExistingId { get; set; }
    public bool RemoveImage { get; set; }
}

public record ToggleStatusRequest(bool IsActive);
