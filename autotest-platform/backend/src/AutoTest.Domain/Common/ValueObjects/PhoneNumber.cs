using System.Text.RegularExpressions;

namespace AutoTest.Domain.Common.ValueObjects;

public partial record PhoneNumber
{
    public string Value { get; }

    public PhoneNumber(string value)
    {
        var digits = DigitsOnly().Replace(value, "");
        if (digits.Length < 9 || digits.Length > 15)
            throw new ArgumentException($"Invalid phone number: {value}");

        Value = digits.StartsWith("998") ? $"+{digits}" : $"+998{digits}";
    }

    public static PhoneNumber From(string value) => new(value);

    public override string ToString() => Value;

    public static implicit operator string(PhoneNumber phone) => phone.Value;

    [GeneratedRegex(@"[^\d]")]
    private static partial Regex DigitsOnly();
}
