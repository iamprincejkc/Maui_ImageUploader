using System.Collections.Generic;
using System.Threading.Tasks;

namespace MauiImageUploader.Services
{
    public interface IImageService
    {
        /// <summary>
        /// Compress a single image to multiple sizes
        /// </summary>
        Task<bool> CompressSingleImageAsync(string imagePath, string outputDirectory);

        /// <summary>
        /// Compress all images in a folder to multiple sizes
        /// </summary>
        Task<BulkProcessingResult> CompressBulkImagesAsync(string inputFolderPath, string outputFolderPath, IProgress<BulkProcessingProgress> progress);

        /// <summary>
        /// Get supported image extensions
        /// </summary>
        IEnumerable<string> GetSupportedImageExtensions();
    }

    public class BulkProcessingResult
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int FailedFiles { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
        public bool IsSuccessful => FailedFiles == 0 && ProcessedFiles > 0;
    }

    public class BulkProcessingProgress
    {
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFileName { get; set; } = string.Empty;
        public double ProgressPercentage => TotalFiles > 0 ? (double)CurrentFile / TotalFiles * 100 : 0;
    }
}