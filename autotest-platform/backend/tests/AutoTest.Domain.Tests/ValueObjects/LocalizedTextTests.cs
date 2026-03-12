using AutoTest.Domain.Common.Enums;
using AutoTest.Domain.Common.ValueObjects;

namespace AutoTest.Domain.Tests.ValueObjects;

public class LocalizedTextTests
{
    [Fact]
    public void Get_ReturnsCorrectLanguageVariant()
    {
        var text = new LocalizedText("Узбекча", "O'zbekcha", "Узбекский");

        Assert.Equal("Узбекча", text.Get(Language.Uz));
        Assert.Equal("O'zbekcha", text.Get(Language.UzLatin));
        Assert.Equal("Узбекский", text.Get(Language.Ru));
    }

    [Fact]
    public void Get_UnknownLanguage_DefaultsToUzLatin()
    {
        var text = new LocalizedText("Uz", "UzLatin", "Ru");
        Assert.Equal("UzLatin", text.Get((Language)99));
    }
}
