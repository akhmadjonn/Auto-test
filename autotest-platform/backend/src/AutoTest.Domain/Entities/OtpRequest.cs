namespace AutoTest.Domain.Entities;

public class OtpRequest
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = null!;
    public string CodeHash { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
