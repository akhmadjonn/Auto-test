namespace AutoTest.Application.Features.ColorVision;

public record ColorVisionPlate(string PlateId, string ImageKey, string ExpectedAnswer, string? AlternateAnswer = null);

public static class ColorVisionPlates
{
    public static readonly List<ColorVisionPlate> All =
    [
        new("plate-01", "color-vision/plate-01.webp", "12"),
        new("plate-02", "color-vision/plate-02.webp", "8"),
        new("plate-03", "color-vision/plate-03.webp", "29"),
        new("plate-04", "color-vision/plate-04.webp", "5"),
        new("plate-05", "color-vision/plate-05.webp", "3"),
        new("plate-06", "color-vision/plate-06.webp", "15"),
        new("plate-07", "color-vision/plate-07.webp", "74"),
        new("plate-08", "color-vision/plate-08.webp", "6"),
        new("plate-09", "color-vision/plate-09.webp", "45"),
        new("plate-10", "color-vision/plate-10.webp", "5"),
        new("plate-11", "color-vision/plate-11.webp", "7"),
        new("plate-12", "color-vision/plate-12.webp", "16")
    ];
}
