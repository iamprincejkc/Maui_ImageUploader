using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;

namespace MauiImageUploader.Views
{
    public partial class ImagePreviewPage : ContentPage
    {
        public ImagePreviewPage()
        {
            InitializeComponent();
        }

        private async void OnSelectImageClicked(object sender, EventArgs e)
        {
            try
            {
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
                    PickerTitle = "Select an image to preview",
                    FileTypes = customFileType,
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    await LoadImagePreview(result.FullPath);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error selecting image: {ex.Message}", "OK");
            }
        }

        private async Task LoadImagePreview(string imagePath)
        {
            try
            {
                // Display the image
                PreviewImage.Source = ImageSource.FromFile(imagePath);
                PreviewImage.IsVisible = true;

                // Get image information
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath);
                var fileInfo = new FileInfo(imagePath);

                var details = $"File Name: {Path.GetFileName(imagePath)}\n" +
                             $"Dimensions: {image.Width} × {image.Height} pixels\n" +
                             $"File Size: {FormatFileSize(fileInfo.Length)}\n" +
                             $"Format: {image.Metadata.DecodedImageFormat?.Name ?? "Unknown"}\n" +
                             $"Created: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}";

                ImageDetailsLabel.Text = details;
                ImageDetailsFrame.IsVisible = true;
                ImageInfoLabel.Text = "Image loaded successfully";
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error loading image: {ex.Message}", "OK");
                ImageInfoLabel.Text = "Failed to load image";
                PreviewImage.IsVisible = false;
                ImageDetailsFrame.IsVisible = false;
            }
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
    }
}