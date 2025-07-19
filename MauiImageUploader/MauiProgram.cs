using MauiImageUploader.Services;
using MauiImageUploader.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using CommunityToolkit.Maui;

namespace MauiImageUploader
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("FontAwesome.otf", "FontAwesome");
                });

            builder.Logging.AddDebug();

            // Register Services
            builder.Services.AddSingleton<IImageService, ImageService>();
            builder.Services.AddSingleton<IFtpService, FtpService>();

            // Register Pages
            builder.Services.AddSingleton<ImageUploaderPage>();
            builder.Services.AddTransient<ImagePreviewPage>();

            return builder.Build();
        }
    }
}