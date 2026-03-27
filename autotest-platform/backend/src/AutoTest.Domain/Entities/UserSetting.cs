namespace AutoTest.Domain.Entities;

public class UserSetting
{
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
