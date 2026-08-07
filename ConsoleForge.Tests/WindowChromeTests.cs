using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Tests.Fakes;
using ConsoleForge.UI.ViewModels;
using ConsoleForge.UI.Views;

namespace ConsoleForge.Tests;

public class WindowChromeTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-chrome-" + Guid.NewGuid().ToString("N"));

    public WindowChromeTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public void WindowUsesFullyCustomChromeAndStaysResizable()
    {
        var window = CreateWindow();

        Assert.True(window.CanResize, "manual grips still need CanResize for BeginResizeDrag");
        Assert.Equal(SystemDecorations.None, window.SystemDecorations);
        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.Equal(ExtendClientAreaChromeHints.NoChrome, window.ExtendClientAreaChromeHints);
    }

    [AvaloniaFact]
    public void TitleBarExposesMinimiseAndCloseOnly()
    {
        var window = CreateWindow();

        var titleBar = window.GetVisualDescendants()
            .OfType<Border>()
            .First(border => border.Name == "TitleBar");

        var buttons = titleBar.GetVisualDescendants().OfType<Button>().ToList();

        Assert.Equal(2, buttons.Count);
        Assert.Contains(buttons, button => button.Name == "MinimizeButton");
        Assert.Contains(buttons, button => button.Name == "CloseButton");
    }

    [AvaloniaTheory]
    [InlineData("GripNorth", WindowEdge.North)]
    [InlineData("GripSouth", WindowEdge.South)]
    [InlineData("GripWest", WindowEdge.West)]
    [InlineData("GripEast", WindowEdge.East)]
    [InlineData("GripNorthWest", WindowEdge.NorthWest)]
    [InlineData("GripNorthEast", WindowEdge.NorthEast)]
    [InlineData("GripSouthWest", WindowEdge.SouthWest)]
    [InlineData("GripSouthEast", WindowEdge.SouthEast)]
    public void EachGripResolvesToItsOwnWindowEdge(string gripName, WindowEdge expected)
    {
        var window = CreateWindow();

        var grip = window.GetVisualDescendants()
            .OfType<Border>()
            .First(border => border.Name == gripName);

        Assert.Equal(expected, MainWindow.ResolveWindowEdge(grip));
        Assert.NotNull(grip.Cursor);
        Assert.Equal(Brushes.Transparent, grip.Background);
    }

    [AvaloniaFact]
    public void AllEightResizeGripsExistAndCoverEveryEdgeAndCorner()
    {
        var window = CreateWindow();

        var edges = window.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => (border.Name ?? string.Empty).StartsWith("Grip", StringComparison.Ordinal))
            .Select(border => MainWindow.ResolveWindowEdge(border))
            .ToList();

        Assert.Equal(8, edges.Count);
        Assert.Equal(8, edges.Distinct().Count());
        Assert.All(edges, edge => Assert.NotNull(edge));
    }

    [AvaloniaFact]
    public void GripsWithoutAValidTagResolveToNothing()
    {
        Assert.Null(MainWindow.ResolveWindowEdge(new Border()));
        Assert.Null(MainWindow.ResolveWindowEdge(new Border { Tag = "Nowhere" }));
        Assert.Null(MainWindow.ResolveWindowEdge(null));
    }

    [AvaloniaFact]
    public void NoMaximiseOrRestoreControlExistsAnywhereInTheWindow()
    {
        var window = CreateWindow();

        var suspects = window.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => (control.Name ?? string.Empty) is { Length: > 0 } name &&
                              (name.Contains("Maximi", StringComparison.OrdinalIgnoreCase) ||
                               name.Contains("Restore", StringComparison.OrdinalIgnoreCase)))
            .Select(control => control.Name)
            .ToList();

        Assert.Empty(suspects);
    }

    [AvaloniaTheory]
    [InlineData(WindowState.Maximized)]
    [InlineData(WindowState.FullScreen)]
    public void MaximisingIsNeutralisedWhicheverWayItIsTriggered(WindowState attempted)
    {
        var window = CreateWindow();

        Assert.Equal(WindowState.Normal, window.WindowState);

        window.WindowState = attempted;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Normal, window.WindowState);
    }

    [AvaloniaFact]
    public void MinimisingIsStillAllowed()
    {
        var window = CreateWindow();

        window.WindowState = WindowState.Minimized;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Minimized, window.WindowState);

        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Normal, window.WindowState);
    }

    [AvaloniaFact]
    public async Task ClosingStillAsksWhenAConversionIsRunning()
    {
        var dialogs = new ScriptedDialogService();
        var converter = new RecordingConverter();
        var viewModel = CreateViewModel(dialogs, converter);

        await viewModel.EnqueueAsync([CreateVideo("a.mkv"), CreateVideo("b.mkv")]);

        var gate = new TaskCompletionSource();
        converter.BlockUntil = gate;

        var run = viewModel.StartCommand.ExecuteAsync(null);
        for (var attempt = 0; attempt < 200 && converter.InFlight is null; attempt++) await Task.Delay(10);

        dialogs.ConfirmAnswers.Enqueue(false);
        Assert.False(await viewModel.ConfirmExitAsync());
        Assert.Single(dialogs.ConfirmPrompts);

        converter.BlockUntil = null;
        gate.TrySetResult();
        await run;
    }

    private MainWindow CreateWindow()
    {
        var window = new MainWindow
        {
            DataContext = CreateViewModel(new ScriptedDialogService(), new RecordingConverter())
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private MainWindowViewModel CreateViewModel(ScriptedDialogService dialogs, RecordingConverter converter)
    {
        var viewModel = new MainWindowViewModel(
            new FakeProbeService(),
            converter,
            new StubEncoderAvailability(),
            new InMemoryConfigStore(),
            dialogs,
            new RecordingThumbnailService(),
            toolchainReady: true,
            toolchainStatus: "test")
        {
            OutputRoot = Path.Combine(_workspace, "out")
        };

        viewModel.SelectedProfile = DeviceProfileCatalog.Psp;
        return viewModel;
    }

    private string CreateVideo(string name)
    {
        var path = Path.Combine(_workspace, name);
        File.WriteAllText(path, "stub");
        return path;
    }
}
