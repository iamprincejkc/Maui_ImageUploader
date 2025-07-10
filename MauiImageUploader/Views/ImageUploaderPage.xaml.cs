using MauiImageUploader.Services;

namespace MauiImageUploader.Views;

public partial class ImageUploaderPage : ContentPage
{
    private readonly IImageService _imageService;

    private Stream? _lastPickedImageStream;
    private string? _sanitizedBase;
    private string? _sanitizedLabel;

    public ImageUploaderPage(IImageService imageService)
    {
        InitializeComponent();
        _imageService = imageService;
    }

    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        try
        {

            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Select an image"
            });

            if (result is not null)
            {
                using var originalStream = await result.OpenReadAsync();
                var memoryStream = new MemoryStream();
                await originalStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                _lastPickedImageStream = memoryStream;

                OriginalImage.Source = ImageSource.FromStream(() => new MemoryStream(memoryStream.ToArray()));
                OriginalImageSmall.Source = OriginalImage.Source;

                StatusLabel.Text = "Image loaded. Click 'Save Image' to process.";
                SaveImageButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void OnSaveImageClicked(object sender, EventArgs e)
    {
        try
        {
            var baseName = BaseNameEntry.Text?.Trim();
            var label = LabelEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(baseName) || string.IsNullOrWhiteSpace(label))
            {
                StatusLabel.Text = "Base name and label are required.";
                return;
            }

            _sanitizedBase = baseName.Replace(" ", "_");
            _sanitizedLabel = label.Replace(" ", "_");

            if (_lastPickedImageStream == null || string.IsNullOrWhiteSpace(_sanitizedBase) || string.IsNullOrWhiteSpace(_sanitizedLabel))
            {
                StatusLabel.Text = "No image loaded.";
                return;
            }

            _lastPickedImageStream.Position = 0;
            await _imageService.ProcessAndSaveImageAsync(_lastPickedImageStream, _sanitizedBase, _sanitizedLabel);

            var basePath = FileSystem.AppDataDirectory;

            string GetPath(string size) => Path.Combine(
                basePath,
                size switch
                {
                    "L" => "large",
                    "M" => "medium",
                    "S" => "small",
                    _ => throw new ArgumentException("Invalid size")
                },
                $"{_sanitizedBase}_{_sanitizedLabel}_{size}.jpg"
            );

            LargeImage.Source = ImageSource.FromFile(GetPath("L"));
            MediumImage.Source = ImageSource.FromFile(GetPath("M"));
            SmallImage.Source = ImageSource.FromFile(GetPath("S"));

            LargeImageSmall.Source = LargeImage.Source;
            MediumImageSmall.Source = MediumImage.Source;
            SmallImageSmall.Source = SmallImage.Source;

            StatusLabel.Text = "Image saved and resized.";
            SaveImageButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Save failed: {ex.Message}";
        }
    }
    private async void OnImageTapped(object sender, EventArgs e)
    {
        if (sender is Image tappedImage && tappedImage.Source != null)
        {
            var previewPage = new ImagePreviewPage(tappedImage.Source);
            await Navigation.PushModalAsync(previewPage);
        }
    }
}
