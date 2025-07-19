using FluentFTP;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace MauiImageUploader.Services
{
    public class FtpService : IFtpService
    {
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };

        private readonly Dictionary<string, int> _compressionSizes = new()
        {
            { "small", 800 },
            { "medium", 1200 },
            { "large", 1920 }
        };

        public FtpTestResult TestConnection(FtpConfiguration config)
        {
            try
            {
                using var client = CreateFtpClient(config);

                // Connect to the FTP server
                client.Connect();

                if (!client.IsConnected)
                {
                    return new FtpTestResult
                    {
                        IsSuccessful = false,
                        Message = "Failed to connect to FTP server",
                        ErrorDetails = "Connection was not established"
                    };
                }

                // Test authentication by getting working directory
                var currentDir = client.GetWorkingDirectory();

                // Test if we can access/create the remote path
                var remotePath = NormalizePath(config.RemotePath);
                if (!string.IsNullOrEmpty(remotePath) && remotePath != "/")
                {
                    var exists = client.DirectoryExists(remotePath);
                    if (!exists)
                    {
                        // Try to create the directory to test write permissions
                        try
                        {
                            client.CreateDirectory(remotePath);
                        }
                        catch (Exception ex)
                        {
                            return new FtpTestResult
                            {
                                IsSuccessful = false,
                                Message = "Cannot access or create remote directory",
                                ErrorDetails = $"Failed to create directory '{remotePath}': {ex.Message}"
                            };
                        }
                    }
                }

                client.Disconnect();

                return new FtpTestResult
                {
                    IsSuccessful = true,
                    Message = $"Connection successful! Current directory: {currentDir}",
                    ErrorDetails = ""
                };
            }
            catch (Exception ex)
            {
                return new FtpTestResult
                {
                    IsSuccessful = false,
                    Message = "Connection failed",
                    ErrorDetails = ex.Message
                };
            }
        }

        public BulkProcessingResult ProcessAndUpload(string inputFolderPath, FtpConfiguration config, IProgress<BulkProcessingProgress> progress)
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

                // Connect to FTP server
                using var ftpClient = CreateFtpClient(config);
                ftpClient.Connect();

                if (!ftpClient.IsConnected)
                {
                    result.ErrorMessages.Add("Failed to connect to FTP server");
                    return result;
                }

                // Ensure remote base directory exists
                var basePath = NormalizePath(config.RemotePath);
                if (!ftpClient.DirectoryExists(basePath))
                {
                    ftpClient.CreateDirectory(basePath);
                }

                // Process each image file
                for (int i = 0; i < imageFiles.Count; i++)
                {
                    var imagePath = imageFiles[i];
                    var fileName = Path.GetFileName(imagePath);
                    var baseName = Path.GetFileNameWithoutExtension(imagePath);

                    try
                    {
                        // Report progress
                        progress?.Report(new BulkProcessingProgress
                        {
                            CurrentFile = i + 1,
                            TotalFiles = result.TotalFiles,
                            CurrentFileName = fileName
                        });

                        var success = ProcessAndUploadSingleImage(ftpClient, imagePath, basePath, baseName);

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

                ftpClient.Disconnect();
            }
            catch (Exception ex)
            {
                result.ErrorMessages.Add($"FTP processing error: {ex.Message}");
            }

            return result;
        }

        private bool ProcessAndUploadSingleImage(FtpClient ftpClient, string imagePath, string basePath, string baseName)
        {
            try
            {
                // Create folder for this image on FTP server
                var imageFolderPath = $"{basePath}/{baseName}".Replace("//", "/");
                ftpClient.CreateDirectory(imageFolderPath);

                // Load and process the image (synchronous)
                using var originalImage = Image.Load(imagePath);

                foreach (var size in _compressionSizes)
                {
                    var remotePath = $"{imageFolderPath}/{baseName}_{size.Key}.webp";

                    // Process image in memory
                    using var processedImage = originalImage.Clone(x => { });
                    var (newWidth, newHeight) = CalculateNewDimensions(processedImage.Width, processedImage.Height, size.Value);
                    processedImage.Mutate(x => x.Resize(newWidth, newHeight));

                    // Convert to WebP in memory
                    using var memoryStream = new MemoryStream();
                    var encoder = new WebpEncoder()
                    {
                        Quality = GetQualityForSize(size.Value)
                    };
                    processedImage.Save(memoryStream, encoder);
                    memoryStream.Position = 0;

                    // Upload to FTP
                    var uploadResult = ftpClient.UploadStream(memoryStream, remotePath, FtpRemoteExists.Overwrite);

                    if (uploadResult == FtpStatus.Failed)
                    {
                        throw new Exception($"Failed to upload {baseName}_{size.Key}.webp");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing {baseName}: {ex.Message}");
                return false;
            }
        }

        private FtpClient CreateFtpClient(FtpConfiguration config)
        {
            var client = new FtpClient(config.Server, config.Username, config.Password, config.Port);

            // Configure client settings
            client.Config.ConnectTimeout = 30000; // 30 seconds
            client.Config.ReadTimeout = 30000;
            client.Config.DataConnectionConnectTimeout = 30000;
            client.Config.DataConnectionReadTimeout = 30000;

            // Use passive mode for better compatibility
            client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;

            // Enable UTF8 for international characters
            client.Config.EncryptionMode = FtpEncryptionMode.None; // Use explicit FTPS if needed
            client.Encoding = System.Text.Encoding.UTF8;

            return client;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            // Ensure path starts with /
            if (!path.StartsWith("/"))
                path = "/" + path;

            // Remove trailing slash unless it's root
            if (path.Length > 1 && path.EndsWith("/"))
                path = path.TrimEnd('/');

            return path;
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