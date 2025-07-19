using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Image = SixLabors.ImageSharp.Image;

namespace MauiImageUploader.Services
{
    public class ImageService : IImageService
    {
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };

        private readonly Dictionary<string, int> _compressionSizes = new()
        {
            { "small", 800 },
            { "medium", 1200 },
            { "large", 1920 }
        };

        public IEnumerable<string> GetSupportedImageExtensions()
        {
            return _supportedExtensions;
        }

        public async Task<bool> CompressSingleImageAsync(string imagePath, string outputDirectory)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return false;

                var fileName = Path.GetFileNameWithoutExtension(imagePath);

                // Create output directory if it doesn't exist
                Directory.CreateDirectory(outputDirectory);

                // Create a subfolder for this image (same as bulk processing)
                var imageFolder = Path.Combine(outputDirectory, fileName);
                Directory.CreateDirectory(imageFolder);

                using var image = await Image.LoadAsync(imagePath);

                foreach (var size in _compressionSizes)
                {
                    var outputPath = Path.Combine(imageFolder, $"{fileName}_{size.Key}.webp");
                    await CompressImageToSizeAsync(image, outputPath, size.Value, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                // Log the exception (you might want to inject ILogger here)
                System.Diagnostics.Debug.WriteLine($"Error compressing image: {ex.Message}");
                return false;
            }
        }

        public async Task<BulkProcessingResult> CompressBulkImagesAsync(string inputFolderPath, string outputFolderPath, IProgress<BulkProcessingProgress> progress)
        {
            var result = new BulkProcessingResult();

            try
            {
                if (!Directory.Exists(inputFolderPath))
                {
                    result.ErrorMessages.Add("Input folder does not exist");
                    return result;
                }

                // Get all image files from the input folder
                var imageFiles = Directory.GetFiles(inputFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();

                result.TotalFiles = imageFiles.Count;

                if (result.TotalFiles == 0)
                {
                    result.ErrorMessages.Add("No supported image files found in the selected folder");
                    return result;
                }

                // Create main output directory
                Directory.CreateDirectory(outputFolderPath);

                // Process each image file
                for (int i = 0; i < imageFiles.Count; i++)
                {
                    var imagePath = imageFiles[i];
                    var fileName = Path.GetFileName(imagePath);

                    try
                    {
                        // Report progress
                        progress?.Report(new BulkProcessingProgress
                        {
                            CurrentFile = i + 1,
                            TotalFiles = result.TotalFiles,
                            CurrentFileName = fileName
                        });

                        var success = await ProcessSingleImageForBulk(imagePath, outputFolderPath);

                        if (success)
                        {
                            result.ProcessedFiles++;
                        }
                        else
                        {
                            result.FailedFiles++;
                            result.ErrorMessages.Add($"Failed to process: {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedFiles++;
                        result.ErrorMessages.Add($"Error processing {fileName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessages.Add($"Bulk processing error: {ex.Message}");
            }

            return result;
        }

        private async Task<bool> ProcessSingleImageForBulk(string imagePath, string outputFolderPath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(imagePath);

                // Create a subfolder for this image
                var imageFolder = Path.Combine(outputFolderPath, fileName);
                Directory.CreateDirectory(imageFolder);

                using var image = await Image.LoadAsync(imagePath);

                foreach (var size in _compressionSizes)
                {
                    var outputPath = Path.Combine(imageFolder, $"{fileName}_{size.Key}.webp");
                    await CompressImageToSizeAsync(image, outputPath, size.Value, true);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task CompressImageToSizeAsync(Image image, string outputPath, int maxDimension, bool useWebP = false)
        {
            using var clone = image.Clone(ctx => { });

            // Calculate new dimensions while maintaining aspect ratio
            var (newWidth, newHeight) = CalculateNewDimensions(clone.Width, clone.Height, maxDimension);

            // Resize the image
            clone.Mutate(x => x.Resize(newWidth, newHeight));

            // Save with appropriate format and compression
            if (useWebP)
            {
                var webpEncoder = new WebpEncoder()
                {
                    Quality = GetQualityForSize(maxDimension)
                };
                await clone.SaveAsync(outputPath, webpEncoder);
            }
            else
            {
                var jpegEncoder = new JpegEncoder()
                {
                    Quality = GetQualityForSize(maxDimension)
                };
                await clone.SaveAsync(outputPath, jpegEncoder);
            }
        }

        private static (int width, int height) CalculateNewDimensions(int originalWidth, int originalHeight, int maxDimension)
        {
            if (originalWidth <= maxDimension && originalHeight <= maxDimension)
                return (originalWidth, originalHeight);

            var aspectRatio = (double)originalWidth / originalHeight;

            if (originalWidth > originalHeight)
            {
                return (maxDimension, (int)(maxDimension / aspectRatio));
            }
            else
            {
                return ((int)(maxDimension * aspectRatio), maxDimension);
            }
        }

        private static int GetQualityForSize(int maxDimension)
        {
            return maxDimension switch
            {
                <= 800 => 75,   // Small - lower quality for smaller file size
                <= 1200 => 85,  // Medium - balanced quality
                _ => 90         // Large - higher quality
            };
        }
    }
}