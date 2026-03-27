using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Entities;

public class FirstAidStep : BaseAuditableEntity
{
    public Guid FirstAidProcedureId { get; set; }
    public int StepOrder { get; set; }
    public LocalizedText Title { get; set; } = null!;
    public LocalizedText Description { get; set; } = null!;
    public string? ImageUrl { get; set; }

    public FirstAidProcedure Procedure { get; set; } = null!;
}
