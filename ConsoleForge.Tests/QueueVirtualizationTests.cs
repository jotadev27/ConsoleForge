using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Tests.Fakes;
using ConsoleForge.UI.ViewModels;
using ConsoleForge.UI.Views;

namespace ConsoleForge.Tests;

public class QueueVirtualizationTests : IDisposable
{
    private const int QueueSize = 200;

    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-virt-" + Guid.NewGuid().ToString("N"));

    private readonly RecordingThumbnailService _thumbnails = new();

    public QueueVirtualizationTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public async Task RealListBoxOnlyRealizesAndGeneratesForOnscreenRows()
    {
        var viewModel = await CreateQueueAsync();

        var window = new MainWindow { DataContext = viewModel, Width = 1440, Height = 860 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var generated = _thumbnails.Requests.Count;

        Assert.True(generated > 0, "no rows were realized, so the virtualization hook is not wired up");
        Assert.True(
            generated < QueueSize / 2,
            $"{generated} of {QueueSize} rows generated — generation is still effectively eager");

        var realizedContainers = window.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Count();

        Assert.True(
            realizedContainers < QueueSize,
            $"{realizedContainers} containers realized — the queue list is not virtualizing");

        window.Close();
    }

    [AvaloniaFact]
    public async Task QueueListUsesAVirtualizingPanel()
    {
        var viewModel = await CreateQueueAsync();

        var window = new MainWindow { DataContext = viewModel, Width = 1440, Height = 860 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var queueList = window.GetVisualDescendants()
            .OfType<ListBox>()
            .First(list => list.Name == "QueueList");

        Assert.IsType<VirtualizingStackPanel>(queueList.ItemsPanelRoot);

        window.Close();
    }

    private async Task<MainWindowViewModel> CreateQueueAsync()
    {
        var viewModel = new MainWindowViewModel(
            new FakeProbeService(),
            new RecordingConverter(),
            new StubEncoderAvailability(),
            new InMemoryConfigStore(),
            new ScriptedDialogService(),
            _thumbnails,
            toolchainReady: true,
            toolchainStatus: "test")
        {
            OutputRoot = Path.Combine(_workspace, "out")
        };

        viewModel.SelectedProfile = DeviceProfileCatalog.Psp;

        await viewModel.EnqueueAsync(
            Enumerable.Range(0, QueueSize).Select(index =>
            {
                var path = Path.Combine(_workspace, $"video_{index:000}.mkv");
                File.WriteAllText(path, "stub");
                return path;
            }));

        Assert.Empty(_thumbnails.Requests);
        return viewModel;
    }
}
