using Avalonia.Headless.XUnit;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Tests.Fakes;
using ConsoleForge.UI.ViewModels;

namespace ConsoleForge.Tests;

public class LazyCoverPreviewTests : IDisposable
{
    private const int QueueSize = 40;
    private const int VisibleRows = 6;

    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-lazy-" + Guid.NewGuid().ToString("N"));

    private readonly RecordingConverter _converter = new();
    private readonly RecordingThumbnailService _thumbnails = new();
    private readonly ScriptedDialogService _dialogs = new();

    public LazyCoverPreviewTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public async Task EnqueueingAloneGeneratesNothing()
    {
        var viewModel = await CreateQueueAsync();

        Assert.Equal(QueueSize, viewModel.Queue.Count);
        Assert.Empty(_thumbnails.Requests);
        Assert.All(viewModel.Queue, item => Assert.True(item.SupportsCover));
        Assert.All(viewModel.Queue, item => Assert.Null(item.CoverImage));
    }

    [AvaloniaFact]
    public async Task OnlyRealizedRowsGenerateAndOffscreenRowsStayUntouched()
    {
        var viewModel = await CreateQueueAsync();

        await RealizeAsync(viewModel, 0, VisibleRows);

        Assert.Equal(VisibleRows, _thumbnails.Requests.Count);

        foreach (var item in viewModel.Queue.Take(VisibleRows))
        {
            Assert.Single(_thumbnails.RequestsFor(item.InputPath));
        }

        foreach (var item in viewModel.Queue.Skip(VisibleRows))
        {
            Assert.Empty(_thumbnails.RequestsFor(item.InputPath));
        }
    }

    [AvaloniaFact]
    public async Task ScrollingDownGeneratesOnlyTheNewlyRealizedRows()
    {
        var viewModel = await CreateQueueAsync();

        await RealizeAsync(viewModel, 0, VisibleRows);
        Unrealize(viewModel, 0, VisibleRows);

        _thumbnails.Requests.Clear();
        await RealizeAsync(viewModel, VisibleRows, VisibleRows);

        Assert.Equal(VisibleRows, _thumbnails.Requests.Count);

        foreach (var item in viewModel.Queue.Skip(2 * VisibleRows))
        {
            Assert.Empty(_thumbnails.RequestsFor(item.InputPath));
        }
    }

    [AvaloniaFact]
    public async Task ScrollingBackOverAGeneratedRowUsesTheCache()
    {
        var viewModel = await CreateQueueAsync();

        await RealizeAsync(viewModel, 0, VisibleRows);
        var firstPass = _thumbnails.Requests.Count;

        Unrealize(viewModel, 0, VisibleRows);
        await RealizeAsync(viewModel, 0, VisibleRows);

        Assert.Equal(firstPass, _thumbnails.Requests.Count);

        foreach (var item in viewModel.Queue.Take(VisibleRows))
        {
            Assert.Single(_thumbnails.RequestsFor(item.InputPath));
        }
    }

    [AvaloniaFact]
    public async Task RepeatedRealizationWithoutUnrealizingGeneratesOnce()
    {
        var viewModel = await CreateQueueAsync();

        await RealizeAsync(viewModel, 0, VisibleRows);
        await RealizeAsync(viewModel, 0, VisibleRows);
        await RealizeAsync(viewModel, 0, VisibleRows);

        Assert.Equal(VisibleRows, _thumbnails.Requests.Count);
    }

    [AvaloniaFact]
    public async Task CustomCoverGeneratesImmediatelyEvenWhenTheRowIsOffscreen()
    {
        var viewModel = await CreateQueueAsync();
        var offscreen = viewModel.Queue[^1];

        _dialogs.ImagesToReturn.Enqueue(CreateFile("cover.png"));
        await viewModel.PickCoverCommand.ExecuteAsync(offscreen);

        var request = Assert.Single(_thumbnails.RequestsFor(offscreen.InputPath));

        Assert.True(request.UsesCustomImage);
        Assert.True(offscreen.HasCustomCover);
    }

    [AvaloniaFact]
    public async Task SwitchingProfileRegeneratesOnlyRealizedRows()
    {
        var viewModel = await CreateQueueAsync();

        await RealizeAsync(viewModel, 0, VisibleRows);
        _thumbnails.Requests.Clear();

        viewModel.SelectedProfile = DeviceProfileCatalog.Ps3;
        await viewModel.RefreshRealizedCoversAsync();

        Assert.Empty(_thumbnails.Requests);
        Assert.All(viewModel.Queue, item => Assert.False(item.SupportsCover));
        Assert.All(viewModel.Queue, item => Assert.Null(item.CoverImage));

        viewModel.SelectedProfile = DeviceProfileCatalog.Psp;
        await viewModel.RefreshRealizedCoversAsync();

        Assert.Equal(VisibleRows, _thumbnails.Requests.Count);

        foreach (var item in viewModel.Queue.Skip(VisibleRows))
        {
            Assert.Empty(_thumbnails.RequestsFor(item.InputPath));
        }
    }

    [AvaloniaFact]
    public async Task ProfileSwitchInvalidatesTheCacheSoRealizedRowsRefresh()
    {
        var viewModel = await CreateQueueAsync();
        var visible = viewModel.Queue[0];

        await RealizeAsync(viewModel, 0, 1);
        Assert.Single(_thumbnails.RequestsFor(visible.InputPath));

        viewModel.SelectedProfile = DeviceProfileCatalog.Vita;
        await viewModel.RefreshRealizedCoversAsync();

        viewModel.SelectedProfile = DeviceProfileCatalog.Psp;
        await viewModel.RefreshRealizedCoversAsync();

        Assert.Equal(2, _thumbnails.RequestsFor(visible.InputPath).Count);
    }

    private async Task<MainWindowViewModel> CreateQueueAsync()
    {
        var viewModel = new MainWindowViewModel(
            new FakeProbeService(),
            _converter,
            new StubEncoderAvailability(),
            new InMemoryConfigStore(),
            _dialogs,
            _thumbnails,
            toolchainReady: true,
            toolchainStatus: "test")
        {
            OutputRoot = Path.Combine(_workspace, "out")
        };

        viewModel.SelectedProfile = DeviceProfileCatalog.Psp;

        await viewModel.EnqueueAsync(
            Enumerable.Range(0, QueueSize).Select(index => CreateFile($"video_{index:00}.mkv")));

        return viewModel;
    }

    private static async Task RealizeAsync(MainWindowViewModel viewModel, int start, int count)
    {
        foreach (var item in viewModel.Queue.Skip(start).Take(count))
        {
            viewModel.NotifyRowRealized(item);
        }

        await viewModel.RefreshRealizedCoversAsync();
    }

    private static void Unrealize(MainWindowViewModel viewModel, int start, int count)
    {
        foreach (var item in viewModel.Queue.Skip(start).Take(count))
        {
            viewModel.NotifyRowUnrealized(item);
        }
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_workspace, name);
        File.WriteAllText(path, "stub");
        return path;
    }
}
