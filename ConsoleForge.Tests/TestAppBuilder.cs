using Avalonia;
using Avalonia.Headless;
using ConsoleForge.Tests;
using ConsoleForge.UI;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace ConsoleForge.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
