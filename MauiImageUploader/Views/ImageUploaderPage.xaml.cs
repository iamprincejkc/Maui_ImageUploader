using MauiImageUploader.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using SixLabors.ImageSharp;
using Image = Microsoft.Maui.Controls.Image;
using Color = Microsoft.Maui.Graphics.Color;

namespace MauiImageUploader.Views
{
    public partial class ImageUploaderPage : ContentPage
    {
        private readonly IImageService _imageService;
        private readonly IFtpService _ftpService;
        private string _selectedImagePath = string.Empty;
        private string _selectedInputFolder = string.Empty;
        private string _selectedOutputFolder = string.Empty;
        private string _selectedFtpInputFolder = string.Empty;
        private bool _isProcessing = false;

        public ImageUploaderPage(IImageService imageService, IFtpService ftpService)
        {
            InitializeComponent();
            _imageService = imageService;
            _ftpService = ftpService;
            LoadSavedFtpConfiguration();
        }

        #region Tab Navigation

        private void OnSingleTabClicked(object sender, EventArgs e)
        {
            SetActiveTab(0);
        }

        private void OnBulkTabClicked(object sender, EventArgs e)
        {
            SetActiveTab(1);
        }

        private void OnFtpTabClicked(object sender, EventArgs e)
        {
            SetActiveTab(2);
        }

        private void SetActiveTab(int activeTabIndex)
        {
            // Reset all tabs to inactive state
            SingleTabButton.BackgroundColor = GetAppThemeColor("Gray300", "Gray600");
            SingleTabButton.TextColor = GetAppThemeColor("Gray900", "Gray100");
            BulkTabButton.BackgroundColor = GetAppThemeColor("Gray300", "Gray600");
            BulkTabButton.TextColor = GetAppThemeColor("Gray900", "Gray100");
            FtpTabButton.BackgroundColor = GetAppThemeColor("Gray300", "Gray600");
            FtpTabButton.TextColor = GetAppThemeColor("Gray900", "Gray100");

            // Hide all content
            SingleImageContent.IsVisible = false;
            BulkImageContent.IsVisible = false;
            FtpImageContent.IsVisible = false;

            // Set active tab and show content
            switch (activeTabIndex)
            {
                case 0: // Single
                    SingleTabButton.BackgroundColor = GetAppThemeColor("Primary", "PrimaryDark");
                    SingleTabButton.TextColor = Colors.White;
                    SingleImageContent.IsVisible = true;
                    break;
                case 1: // Bulk
                    BulkTabButton.BackgroundColor = GetAppThemeColor("Primary", "PrimaryDark");
                    BulkTabButton.TextColor = Colors.White;
                    BulkImageContent.IsVisible = true;
                    break;
                case 2: // FTP
                    FtpTabButton.BackgroundColor = GetAppThemeColor("Primary", "PrimaryDark");
                    FtpTabButton.TextColor = Colors.White;
                    FtpImageContent.IsVisible = true;
                    break;
            }
        }

        private Color GetAppThemeColor(string lightColor, string darkColor)
        {
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? GetResourceColor(darkColor)
                : GetResourceColor(lightColor);
        }

        private Color GetResourceColor(string resourceKey)
        {
            if (Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is Color color)
                return color;
            return Colors.Gray;
        }

        #endregion

        #region Single Image Processing

        private async void OnSelectImageClicked(object sender, EventArgs e)
        {
            try
            {
                // Hide previous preview
                ImagePreviewFrame.IsVisible = false;

                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.image" } },
                        { DevicePlatform.Android, new[] { "image/*" } },
                        { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" } },
                        { DevicePlatform.macOS, new[] { "public.image" } }
                    });

                var options = new PickOptions
                {
                    PickerTitle = "Select an image",
                    FileTypes = customFileType,
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    _selectedImagePath = result.FullPath;
                    SelectedImageLabel.Text = $"Selected: {Path.GetFileName(_selectedImagePath)}";
                    SelectedImageLabel.IsVisible = true;
                    ProcessSingleButton.IsVisible = true;

                    // Show image preview
                    await LoadImagePreview(_selectedImagePath);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error selecting image: {ex.Message}", "OK");
            }
        }

        private async void OnProcessSingleClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedImagePath) || _isProcessing)
                return;

            try
            {
                _isProcessing = true;
                ProcessSingleButton.IsEnabled = false;
                SingleProgressBar.IsVisible = true;
                SingleStatusLabel.IsVisible = true;
                SingleStatusLabel.Text = "Processing image...";
                SingleProgressBar.Progress = 0.5;
                CompressionResultsFrame.IsVisible = false;

                // Select output directory
                var result = await DisplayActionSheet("Select Output Method", "Cancel", null, "Choose Folder", "Use Desktop");
                if (result == "Cancel")
                {
                    SingleStatusLabel.Text = "Output folder selection cancelled.";
                    return;
                }

                string outputPath;
                if (result == "Choose Folder")
                {
                    try
                    {
                        var folderResult = await FolderPicker.Default.PickAsync();
                        if (folderResult?.Folder?.Path == null)
                        {
                            SingleStatusLabel.Text = "Output folder selection cancelled.";
                            return;
                        }
                        outputPath = folderResult.Folder.Path;
                    }
                    catch
                    {
                        // Fallback to desktop if FolderPicker fails
                        outputPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    }
                }
                else
                {
                    outputPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                var success = await _imageService.CompressSingleImageAsync(_selectedImagePath, outputPath);

                SingleProgressBar.Progress = 1.0;

                if (success)
                {
                    SingleStatusLabel.Text = "✅ Image processed successfully!";
                    SingleStatusLabel.TextColor = Colors.Green;

                    // Show compression results with previews
                    await ShowCompressionResults(outputPath);

                    await DisplayAlert("Success", "Image compressed successfully! Check the preview below.", "OK");
                }
                else
                {
                    SingleStatusLabel.Text = "❌ Failed to process image.";
                    SingleStatusLabel.TextColor = Colors.Red;
                    await DisplayAlert("Error", "Failed to process the image. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                SingleStatusLabel.Text = "❌ Error occurred during processing.";
                SingleStatusLabel.TextColor = Colors.Red;
                await DisplayAlert("Error", $"Error processing image: {ex.Message}", "OK");
            }
            finally
            {
                _isProcessing = false;
                ProcessSingleButton.IsEnabled = true;

                // Hide progress after a delay
                await Task.Delay(3000);
                SingleProgressBar.IsVisible = false;
                if (SingleStatusLabel.Text.Contains("✅") || SingleStatusLabel.Text.Contains("❌"))
                {
                    SingleStatusLabel.IsVisible = false;
                }
            }
        }

        #endregion

        #region Image Preview and Popup

        private async Task LoadImagePreview(string imagePath)
        {
            try
            {
                // Display the image
                PreviewImageSingle.Source = ImageSource.FromFile(imagePath);

                // Get image information
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath);
                var fileInfo = new FileInfo(imagePath);

                var details = $"{image.Width} × {image.Height} px • {FormatFileSize(fileInfo.Length)} • {image.Metadata.DecodedImageFormat?.Name ?? "Unknown"}";
                ImageDetailsLabelSingle.Text = details;

                ImagePreviewFrame.IsVisible = true;
            }
            catch (Exception ex)
            {
                ImagePreviewFrame.IsVisible = false;
                System.Diagnostics.Debug.WriteLine($"Error loading image preview: {ex.Message}");
            }
        }

        private async Task ShowCompressionResults(string outputBasePath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(_selectedImagePath);
                var imageFolderPath = Path.Combine(outputBasePath, fileName);

                if (!Directory.Exists(imageFolderPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Output folder not found: {imageFolderPath}");
                    return;
                }

                // Load original image
                OriginalPreview.Source = ImageSource.FromFile(_selectedImagePath);
                using var originalImage = await SixLabors.ImageSharp.Image.LoadAsync(_selectedImagePath);
                var originalFileInfo = new FileInfo(_selectedImagePath);
                OriginalSizeLabel.Text = $"{originalImage.Width}×{originalImage.Height}\n{FormatFileSize(originalFileInfo.Length)}";

                // Load compressed images
                var largeFile = Path.Combine(imageFolderPath, $"{fileName}_large.webp");
                var mediumFile = Path.Combine(imageFolderPath, $"{fileName}_medium.webp");
                var smallFile = Path.Combine(imageFolderPath, $"{fileName}_small.webp");

                long totalOriginalSize = originalFileInfo.Length;
                long totalCompressedSize = 0;

                if (File.Exists(largeFile))
                {
                    LargePreview.Source = ImageSource.FromFile(largeFile);
                    using var largeImage = await SixLabors.ImageSharp.Image.LoadAsync(largeFile);
                    var largeFileInfo = new FileInfo(largeFile);
                    totalCompressedSize += largeFileInfo.Length;
                    LargeSizeLabel.Text = $"{largeImage.Width}×{largeImage.Height}\n{FormatFileSize(largeFileInfo.Length)}";
                }

                if (File.Exists(mediumFile))
                {
                    MediumPreview.Source = ImageSource.FromFile(mediumFile);
                    using var mediumImage = await SixLabors.ImageSharp.Image.LoadAsync(mediumFile);
                    var mediumFileInfo = new FileInfo(mediumFile);
                    totalCompressedSize += mediumFileInfo.Length;
                    MediumSizeLabel.Text = $"{mediumImage.Width}×{mediumImage.Height}\n{FormatFileSize(mediumFileInfo.Length)}";
                }

                if (File.Exists(smallFile))
                {
                    SmallPreview.Source = ImageSource.FromFile(smallFile);
                    using var smallImage = await SixLabors.ImageSharp.Image.LoadAsync(smallFile);
                    var smallFileInfo = new FileInfo(smallFile);
                    totalCompressedSize += smallFileInfo.Length;
                    SmallSizeLabel.Text = $"{smallImage.Width}×{smallImage.Height}\n{FormatFileSize(smallFileInfo.Length)}";
                }

                // Calculate compression summary
                var compressionRatio = totalOriginalSize > 0 ? (double)totalCompressedSize / totalOriginalSize : 0;
                var savedSpace = totalOriginalSize - totalCompressedSize;
                var savedPercentage = totalOriginalSize > 0 ? (1 - compressionRatio) * 100 : 0;

                CompressionSummaryLabel.Text = $"Total space saved: {FormatFileSize(savedSpace)} ({savedPercentage:F1}%)\nOutput folder: {fileName}/";

                CompressionResultsFrame.IsVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing compression results: {ex.Message}");
            }
        }

        private async void OnPreviewImageTapped(object sender, EventArgs e)
        {
            try
            {
                var image = sender as Image;
                if (image?.Source is FileImageSource fileSource)
                {
                    // Determine which image was tapped and set appropriate title
                    string title = "Image Preview";
                    string details = "";

                    if (image == OriginalPreview)
                    {
                        title = "Original Image";
                        details = OriginalSizeLabel.Text;
                    }
                    else if (image == LargePreview)
                    {
                        title = "Large (1920px) - WebP";
                        details = LargeSizeLabel.Text;
                    }
                    else if (image == MediumPreview)
                    {
                        title = "Medium (1200px) - WebP";
                        details = MediumSizeLabel.Text;
                    }
                    else if (image == SmallPreview)
                    {
                        title = "Small (800px) - WebP";
                        details = SmallSizeLabel.Text;
                    }

                    // Set popup content
                    PopupImageTitle.Text = title;
                    PopupImage.Source = image.Source;
                    PopupImageDetails.Text = details;

                    // Show popup
                    ImagePopupModal.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing image popup: {ex.Message}");
            }
        }

        private void OnCloseImagePopup(object sender, EventArgs e)
        {
            ImagePopupModal.IsVisible = false;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        #endregion

        #region Bulk Image Processing

        private async void OnSelectFolderClicked(object sender, EventArgs e)
        {
            try
            {
                FolderPickerResult? result = null;
                try
                {
                    result = await FolderPicker.Default.PickAsync();
                }
                catch
                {
                    // Fallback: Use file picker to select a representative file from the folder
                    var filePickerResult = await DisplayActionSheet("Folder picker not available", "Cancel", null, "Select any image from the folder");
                    if (filePickerResult == "Cancel")
                        return;

                    var customFileType = new FilePickerFileType(
                        new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            { DevicePlatform.iOS, new[] { "public.image" } },
                            { DevicePlatform.Android, new[] { "image/*" } },
                            { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" } },
                            { DevicePlatform.macOS, new[] { "public.image" } }
                        });

                    var options = new PickOptions
                    {
                        PickerTitle = "Select any image from the folder you want to process",
                        FileTypes = customFileType,
                    };

                    var fileResult = await FilePicker.Default.PickAsync(options);
                    if (fileResult != null)
                    {
                        _selectedInputFolder = Path.GetDirectoryName(fileResult.FullPath) ?? string.Empty;
                    }
                    else
                    {
                        return;
                    }
                }

                string folderPath = result?.Folder?.Path ?? _selectedInputFolder;

                if (!string.IsNullOrEmpty(folderPath))
                {
                    _selectedInputFolder = folderPath;
                    var folderName = Path.GetFileName(_selectedInputFolder.TrimEnd(Path.DirectorySeparatorChar));

                    // Count image files
                    var imageFiles = Directory.GetFiles(_selectedInputFolder, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(file => _imageService.GetSupportedImageExtensions().Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToList();

                    SelectedFolderLabel.Text = $"Selected: {folderName} ({imageFiles.Count} images found)";
                    SelectedFolderLabel.IsVisible = true;

                    // Show output selection options
                    SelectOutputFolderButton.IsVisible = true;
                    OutputPathLabel.IsVisible = true;
                    OutputPathEntry.IsVisible = true;

                    // Clear previous output selection
                    _selectedOutputFolder = string.Empty;
                    OutputFolderInfoLabel.IsVisible = false;
                    ProcessBulkButton.IsVisible = false;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error selecting folder: {ex.Message}", "OK");
            }
        }

        private async void OnSelectOutputFolderClicked(object sender, EventArgs e)
        {
            try
            {
                FolderPickerResult? result = null;
                try
                {
                    result = await FolderPicker.Default.PickAsync();
                }
                catch
                {
                    // Fallback to desktop
                    _selectedOutputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    UpdateOutputFolderInfo();
                    return;
                }

                if (result?.Folder?.Path != null)
                {
                    _selectedOutputFolder = result.Folder.Path;
                    OutputPathEntry.Text = _selectedOutputFolder;
                    UpdateOutputFolderInfo();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error selecting output folder: {ex.Message}", "OK");
            }
        }

        private void OnOutputPathChanged(object sender, TextChangedEventArgs e)
        {
            var entry = sender as Entry;
            var path = entry?.Text?.Trim();

            // Show validate button if path looks like a valid path
            if (!string.IsNullOrEmpty(path) && (path.Contains("\\") || path.Contains("/")) && path.Length > 3)
            {
                ValidateOutputPathButton.IsVisible = true;
            }
            else
            {
                ValidateOutputPathButton.IsVisible = false;
            }
        }

        private async void OnValidateOutputPathClicked(object sender, EventArgs e)
        {
            var path = OutputPathEntry.Text?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                await DisplayAlert("Error", "Please enter an output folder path.", "OK");
                return;
            }

            try
            {
                // Create directory if it doesn't exist
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                _selectedOutputFolder = path;
                ValidateOutputPathButton.IsVisible = false;
                UpdateOutputFolderInfo();

                await DisplayAlert("Success", "Output folder validated and ready!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not create or access the output folder: {ex.Message}", "OK");
            }
        }

        private void UpdateOutputFolderInfo()
        {
            if (!string.IsNullOrEmpty(_selectedOutputFolder))
            {
                var outputFolderName = Path.GetFileName(_selectedOutputFolder.TrimEnd(Path.DirectorySeparatorChar));
                OutputFolderInfoLabel.Text = $"Output: {outputFolderName}";
                OutputFolderInfoLabel.IsVisible = true;

                // Enable processing if both input and output are selected
                if (!string.IsNullOrEmpty(_selectedInputFolder))
                {
                    ProcessBulkButton.IsVisible = true;
                }
            }
        }

        private async void OnProcessBulkClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedInputFolder) || string.IsNullOrEmpty(_selectedOutputFolder) || _isProcessing)
                return;

            try
            {
                _isProcessing = true;
                ProcessBulkButton.IsEnabled = false;
                BulkProgressBar.IsVisible = true;
                BulkStatusLabel.IsVisible = true;
                BulkStatusLabel.Text = "Starting bulk processing...";
                BulkResultsFrame.IsVisible = false;

                var progress = new Progress<BulkProcessingProgress>(UpdateBulkProgress);

                var result = await _imageService.CompressBulkImagesAsync(_selectedInputFolder, _selectedOutputFolder, progress);

                // Show final results
                BulkProgressBar.Progress = 1.0;
                ShowBulkResults(result);
            }
            catch (Exception ex)
            {
                BulkStatusLabel.Text = "❌ Error occurred during bulk processing.";
                BulkStatusLabel.TextColor = Colors.Red;
                await DisplayAlert("Error", $"Error during bulk processing: {ex.Message}", "OK");
            }
            finally
            {
                _isProcessing = false;
                ProcessBulkButton.IsEnabled = true;
            }
        }

        private void UpdateBulkProgress(BulkProcessingProgress progress)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BulkProgressBar.Progress = progress.ProgressPercentage / 100.0;
                BulkStatusLabel.Text = $"Processing {progress.CurrentFile}/{progress.TotalFiles}: {progress.CurrentFileName}";
            });
        }

        private async void ShowBulkResults(BulkProcessingResult result)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BulkResultsFrame.IsVisible = true;

                if (result.IsSuccessful)
                {
                    BulkStatusLabel.Text = "✅ Bulk processing completed!";
                    BulkStatusLabel.TextColor = Colors.Green;
                    BulkResultsLabel.Text = $"✅ Successfully processed {result.ProcessedFiles} images";
                    BulkResultsLabel.TextColor = Colors.Green;
                }
                else
                {
                    BulkStatusLabel.Text = "⚠️ Bulk processing completed with issues";
                    BulkStatusLabel.TextColor = Colors.Orange;
                    BulkResultsLabel.Text = $"Processed: {result.ProcessedFiles}, Failed: {result.FailedFiles}";
                    BulkResultsLabel.TextColor = Colors.Orange;
                }

                BulkDetailsLabel.Text = $"Total files: {result.TotalFiles}\n" +
                                       $"Successful: {result.ProcessedFiles}\n" +
                                       $"Failed: {result.FailedFiles}";

                if (result.ErrorMessages.Any())
                {
                    BulkDetailsLabel.Text += $"\n\nErrors:\n{string.Join("\n", result.ErrorMessages.Take(5))}";
                    if (result.ErrorMessages.Count > 5)
                    {
                        BulkDetailsLabel.Text += $"\n... and {result.ErrorMessages.Count - 5} more";
                    }
                }
            });

            // Show completion alert
            var message = result.IsSuccessful
                ? $"Successfully processed {result.ProcessedFiles} images into WebP format with individual subfolders!"
                : $"Processed {result.ProcessedFiles} images with {result.FailedFiles} failures. Check the details below.";

            await DisplayAlert(result.IsSuccessful ? "Success" : "Completed with Issues", message, "OK");
        }

        #endregion

        #region FTP Processing

        private async void OnTestConnectionClicked(object sender, EventArgs e)
        {
            try
            {
                TestConnectionButton.IsEnabled = false;
                TestConnectionButton.Text = "⏳ Testing...";
                ConnectionStatusLabel.IsVisible = true;
                ConnectionStatusLabel.TextColor = Colors.Orange;
                ConnectionStatusLabel.Text = "Testing connection...";

                var server = FtpServerEntry.Text?.Trim();
                var portText = FtpPortEntry.Text?.Trim();
                var username = FtpUsernameEntry.Text?.Trim();
                var password = FtpPasswordEntry.Text?.Trim();

                if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    ConnectionStatusLabel.TextColor = Colors.Red;
                    ConnectionStatusLabel.Text = "❌ Please fill in all required fields";
                    return;
                }

                if (!int.TryParse(portText, out int port) || port <= 0)
                {
                    port = 21; // Default FTP port
                }

                // Create FTP configuration
                var ftpConfig = new FtpConfiguration
                {
                    Server = server,
                    Port = port,
                    Username = username,
                    Password = password,
                    RemotePath = FtpRemotePathEntry.Text?.Trim() ?? "/"
                };

                // Test real FTP connection
                var testResult = _ftpService.TestConnection(ftpConfig);

                if (testResult.IsSuccessful)
                {
                    ConnectionStatusLabel.TextColor = Colors.Green;
                    ConnectionStatusLabel.Text = $"✅ {testResult.Message}";

                    // Enable processing if connection is successful
                    if (!string.IsNullOrEmpty(_selectedFtpInputFolder))
                    {
                        ProcessFtpButton.IsVisible = true;
                    }
                }
                else
                {
                    ConnectionStatusLabel.TextColor = Colors.Red;
                    ConnectionStatusLabel.Text = $"❌ {testResult.Message}";

                    // Show detailed error if available
                    if (!string.IsNullOrEmpty(testResult.ErrorDetails))
                    {
                        await DisplayAlert("Connection Failed",
                            $"{testResult.Message}\n\nDetails: {testResult.ErrorDetails}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                ConnectionStatusLabel.TextColor = Colors.Red;
                ConnectionStatusLabel.Text = $"❌ Connection failed: {ex.Message}";
                await DisplayAlert("Error", $"Unexpected error during connection test: {ex.Message}", "OK");
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
                TestConnectionButton.Text = "🔗 Test Connection";
            }
        }

        private async void OnSelectFtpInputFolderClicked(object sender, EventArgs e)
        {
            try
            {
                FolderPickerResult? result = null;
                try
                {
                    result = await FolderPicker.Default.PickAsync();
                }
                catch
                {
                    // Fallback: Use file picker to select a representative file from the folder
                    var filePickerResult = await DisplayActionSheet("Folder picker not available", "Cancel", null, "Select any image from the folder");
                    if (filePickerResult == "Cancel")
                        return;

                    var customFileType = new FilePickerFileType(
                        new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            { DevicePlatform.iOS, new[] { "public.image" } },
                            { DevicePlatform.Android, new[] { "image/*" } },
                            { DevicePlatform.WinUI, new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" } },
                            { DevicePlatform.macOS, new[] { "public.image" } }
                        });

                    var options = new PickOptions
                    {
                        PickerTitle = "Select any image from the folder you want to process",
                        FileTypes = customFileType,
                    };

                    var fileResult = await FilePicker.Default.PickAsync(options);
                    if (fileResult != null)
                    {
                        _selectedFtpInputFolder = Path.GetDirectoryName(fileResult.FullPath) ?? string.Empty;
                    }
                    else
                    {
                        return;
                    }
                }

                string folderPath = result?.Folder?.Path ?? _selectedFtpInputFolder;

                if (!string.IsNullOrEmpty(folderPath))
                {
                    _selectedFtpInputFolder = folderPath;
                    var folderName = Path.GetFileName(_selectedFtpInputFolder.TrimEnd(Path.DirectorySeparatorChar));

                    // Count image files
                    var imageFiles = Directory.GetFiles(_selectedFtpInputFolder, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(file => _imageService.GetSupportedImageExtensions().Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToList();

                    SelectedFtpFolderLabel.Text = $"Selected: {folderName} ({imageFiles.Count} images found)";
                    SelectedFtpFolderLabel.IsVisible = true;

                    // Enable processing if connection was tested successfully
                    if (ConnectionStatusLabel.Text?.Contains("✅") == true)
                    {
                        ProcessFtpButton.IsVisible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error selecting folder: {ex.Message}", "OK");
            }
        }

        private async void OnProcessFtpClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFtpInputFolder) || _isProcessing)
                return;

            try
            {
                _isProcessing = true;
                ProcessFtpButton.IsEnabled = false;
                FtpProgressBar.IsVisible = true;
                FtpStatusLabel.IsVisible = true;
                FtpStatusLabel.Text = "Starting FTP processing...";
                FtpResultsFrame.IsVisible = false;

                // Save configuration if requested
                if (SaveConfigCheckBox.IsChecked)
                {
                    await SaveFtpConfiguration();
                }

                var progress = new Progress<BulkProcessingProgress>(UpdateFtpProgress);

                // Create FTP configuration object
                var ftpConfig = new FtpConfiguration
                {
                    Server = FtpServerEntry.Text?.Trim() ?? "",
                    Port = int.TryParse(FtpPortEntry.Text?.Trim(), out int port) ? port : 21,
                    Username = FtpUsernameEntry.Text?.Trim() ?? "",
                    Password = FtpPasswordEntry.Text?.Trim() ?? "",
                    RemotePath = FtpRemotePathEntry.Text?.Trim() ?? "/"
                };

                // Validate configuration
                if (string.IsNullOrEmpty(ftpConfig.Server) || string.IsNullOrEmpty(ftpConfig.Username) || string.IsNullOrEmpty(ftpConfig.Password))
                {
                    FtpStatusLabel.Text = "❌ Please fill in all FTP configuration fields";
                    FtpStatusLabel.TextColor = Colors.Red;
                    await DisplayAlert("Configuration Error", "Please ensure all FTP configuration fields are filled in.", "OK");
                    return;
                }

                // Process and upload to FTP using real service
                var result = _ftpService.ProcessAndUpload(_selectedFtpInputFolder, ftpConfig, progress);

                // Show final results
                FtpProgressBar.Progress = 1.0;
                ShowFtpResults(result);
            }
            catch (Exception ex)
            {
                FtpStatusLabel.Text = "❌ Error occurred during FTP processing.";
                FtpStatusLabel.TextColor = Colors.Red;
                await DisplayAlert("Error", $"Error during FTP processing: {ex.Message}", "OK");
            }
            finally
            {
                _isProcessing = false;
                ProcessFtpButton.IsEnabled = true;
            }
        }

        private void UpdateFtpProgress(BulkProcessingProgress progress)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FtpProgressBar.Progress = progress.ProgressPercentage / 100.0;
                FtpStatusLabel.Text = $"Processing & Uploading {progress.CurrentFile}/{progress.TotalFiles}: {progress.CurrentFileName}";
            });
        }

        private async void ShowFtpResults(BulkProcessingResult result)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FtpResultsFrame.IsVisible = true;

                if (result.IsSuccessful)
                {
                    FtpStatusLabel.Text = "✅ FTP upload completed!";
                    FtpStatusLabel.TextColor = Colors.Green;
                    FtpResultsLabel.Text = $"✅ Successfully uploaded {result.ProcessedFiles} images to FTP";
                    FtpResultsLabel.TextColor = Colors.Green;
                }
                else
                {
                    FtpStatusLabel.Text = "⚠️ FTP upload completed with issues";
                    FtpStatusLabel.TextColor = Colors.Orange;
                    FtpResultsLabel.Text = $"Uploaded: {result.ProcessedFiles}, Failed: {result.FailedFiles}";
                    FtpResultsLabel.TextColor = Colors.Orange;
                }

                FtpDetailsLabel.Text = $"Total files: {result.TotalFiles}\n" +
                                      $"Successful: {result.ProcessedFiles}\n" +
                                      $"Failed: {result.FailedFiles}\n" +
                                      $"Remote location: {FtpRemotePathEntry.Text}";

                if (result.ErrorMessages.Any())
                {
                    FtpDetailsLabel.Text += $"\n\nErrors:\n{string.Join("\n", result.ErrorMessages.Take(5))}";
                    if (result.ErrorMessages.Count > 5)
                    {
                        FtpDetailsLabel.Text += $"\n... and {result.ErrorMessages.Count - 5} more";
                    }
                }
            });

            // Show completion alert
            var message = result.IsSuccessful
                ? $"Successfully processed and uploaded {result.ProcessedFiles} images to FTP server!"
                : $"Processed {result.ProcessedFiles} images with {result.FailedFiles} failures. Check the details below.";

            await DisplayAlert(result.IsSuccessful ? "Success" : "Completed with Issues", message, "OK");
        }

        private async Task SaveFtpConfiguration()
        {
            try
            {
                // Save to app preferences (simplified - in production, use secure storage)
                var config = new Dictionary<string, string>
                {
                    ["FtpServer"] = FtpServerEntry.Text?.Trim() ?? "",
                    ["FtpPort"] = FtpPortEntry.Text?.Trim() ?? "21",
                    ["FtpUsername"] = FtpUsernameEntry.Text?.Trim() ?? "",
                    ["FtpRemotePath"] = FtpRemotePathEntry.Text?.Trim() ?? "/"
                };

                // Only save password if remember password is checked
                if (RememberPasswordCheckBox.IsChecked)
                {
                    config["FtpPassword"] = FtpPasswordEntry.Text?.Trim() ?? "";
                }

                // In a real app, save these to secure storage
                foreach (var item in config)
                {
                    Preferences.Set(item.Key, item.Value);
                }

                await DisplayAlert("Configuration Saved", "FTP configuration has been saved for future use.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save configuration: {ex.Message}", "OK");
            }
        }

        private void LoadSavedFtpConfiguration()
        {
            try
            {
                FtpServerEntry.Text = Preferences.Get("FtpServer", "");
                FtpPortEntry.Text = Preferences.Get("FtpPort", "21");
                FtpUsernameEntry.Text = Preferences.Get("FtpUsername", "");
                FtpRemotePathEntry.Text = Preferences.Get("FtpRemotePath", "/");

                if (!string.IsNullOrEmpty(Preferences.Get("FtpPassword", "")))
                {
                    FtpPasswordEntry.Text = Preferences.Get("FtpPassword", "");
                    RememberPasswordCheckBox.IsChecked = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading FTP configuration: {ex.Message}");
            }
        }

        #endregion
    }
}