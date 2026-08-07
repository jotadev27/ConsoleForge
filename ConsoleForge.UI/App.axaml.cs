using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ConsoleForge.Infrastructure.FFmpeg;
using ConsoleForge.Infrastructure.Storage;
using ConsoleForge.UI.Services;
using ConsoleForge.UI.ViewModels;
using ConsoleForge.UI.Views;

namespace ConsoleForge.UI;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configStore = new JsonConfigStore();
            var config = configStore.Load();
            var locator = new FFmpegLocator(config.FFmpegPath, config.FFprobePath);

            var window = new MainWindow();

            var viewModel = new MainWindowViewModel(
                new FFprobeService(locator),
                new FFmpegVideoConverter(locator),
                new FFmpegEncoderAvailability(locator),
                configStore,
                new StorageDialogService(window),
                new FFmpegThumbnailService(locator),
                locator.IsAvailable,
                DescribeToolchain(locator));

            window.DataContext = viewModel;
            window.Opened += async (_, _) => await viewModel.InitializeAsync();

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string DescribeToolchain(FFmpegLocator locator) =>
        locator.IsAvailable
            ? $"ffmpeg: {locator.FFmpegPath}"
            : "ffmpeg/ffprobe NOT FOUND on PATH - conversion is disabled.";
}
