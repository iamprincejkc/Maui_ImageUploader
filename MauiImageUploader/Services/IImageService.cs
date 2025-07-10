namespace MauiImageUploader.Services;
public interface IImageService
{
    Task ProcessAndSaveImageAsync(Stream inputStream, string baseName, string label);
}
