using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Image = SixLabors.ImageSharp.Image;
using ResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;
using Size = SixLabors.ImageSharp.Size;

namespace MauiImageUploader.Services;

public class ImageService : IImageService
{
    public async Task ProcessAndSaveImageAsync(Stream inputStream, string baseName, string label)
    {
        inputStream.Position = 0;
        using var image = await Image.LoadAsync(inputStream);

        var sizes = new[]
        {
            new { Width = 1024, Folder = "large", Suffix = "L" },
            new { Width = 512, Folder = "medium", Suffix = "M" },
            new { Width = 256, Folder = "small", Suffix = "S" }
        };

        var basePath = FileSystem.AppDataDirectory;

        foreach (var size in sizes)
        {
            var resized = image.Clone(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(size.Width, 0)
            }));

            var folderPath = Path.Combine(basePath, size.Folder);
            Directory.CreateDirectory(folderPath);

            var fileName = $"{baseName}_{label}_{size.Suffix}.jpg";
            var fullPath = Path.Combine(folderPath, fileName);

            await using var output = File.Create(fullPath);
            await resized.SaveAsJpegAsync(output, new JpegEncoder { Quality = 85 });
        }
    }
}
