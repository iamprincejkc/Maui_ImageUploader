namespace MauiImageUploader.Views;

public partial class ImagePreviewPage : ContentPage
{
    public ImagePreviewPage(ImageSource imageSource)
    {
        InitializeComponent();
        PreviewImage.Source = imageSource;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
