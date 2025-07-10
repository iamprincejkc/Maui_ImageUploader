using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace MauiImageUploader
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
            => new(new AppShell());   // Shell becomes the window’s root
    }
}