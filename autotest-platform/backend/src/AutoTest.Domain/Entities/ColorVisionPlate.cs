namespace AutoTest.Domain.Entities;

public class ColorVisionPlate : BaseAuditableEntity
{
    public int PlateNumber { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ExpectedAnswer { get; set; } = string.Empty;
    public string? AlternateAnswer { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
