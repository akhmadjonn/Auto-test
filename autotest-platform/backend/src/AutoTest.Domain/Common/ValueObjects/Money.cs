namespace AutoTest.Domain.Common.ValueObjects;

public record Money(long Amount, string Currency = "UZS");
