using AutoTest.Domain.Common.Enums;

namespace AutoTest.Domain.Common.ValueObjects;

public record LocalizedText(string Uz, string UzLatin, string Ru)
{
    public string Get(Language lang) => lang switch
    {
        Language.Uz => Uz,
        Language.UzLatin => UzLatin,
        Language.Ru => Ru,
        _ => UzLatin
    };
}
