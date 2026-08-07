using Avalonia.Headless.XUnit;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Tests.Fakes;
using ConsoleForge.UI.ViewModels;

namespace ConsoleForge.Tests;

public class CoverSelectionTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-cover-" + Guid.NewGuid().ToString("N"));

    private readonly RecordingConverter _converter = new();
    private readonly RecordingThumbnailService _thumbnails = new();
    private readonly ScriptedDialogService _dialogs = new();

    public CoverSelectionTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public async Task ChoosingACoverAssociatesItWithOnlyThatQueueItem()
    {
        var viewModel = CreateViewModel();
        var (first, second) = await AddTwoItemsAsync(viewModel);

        _dialogs.ImagesToReturn.Enqueue(CreateImage("cover-a.png"));
        await viewModel.PickCoverCommand.ExecuteAsync(first);

        Assert.True(first.HasCustomCover);
        Assert.EndsWith("cover-a.png", first.CustomCoverPath);

        Assert.False(second.HasCustomCover);
        Assert.Null(second.CustomCoverPath);
    }

    [AvaloniaFact]
    public async Task EachQueueItemKeepsItsOwnCover()
    {
        var viewModel = CreateViewModel();
        var (first, second) = await AddTwoItemsAsync(viewModel);

        _dialogs.ImagesToReturn.Enqueue(CreateImage("cover-a.png"));
        await viewModel.PickCoverCommand.ExecuteAsync(first);

        _dialogs.ImagesToReturn.Enqueue(CreateImage("cover-b.png"));
        await viewModel.PickCoverCommand.ExecuteAsync(second);

        Assert.EndsWith("cover-a.png", first.CustomCoverPath);
        Assert.EndsWith("cover-b.png", second.CustomCoverPath);
    }

    [AvaloniaFact]
    public async Task CustomCoverReplacesTheAutoFrameInTheFinalThumbnail()
    {
        var viewModel = CreateViewModel();
        var (first, second) = await AddTwoItemsAsync(viewModel);

        var coverPath = CreateImage("cover-a.png");
        _dialogs.ImagesToReturn.Enqueue(coverPath);
        await viewModel.PickCoverCommand.ExecuteAsync(first);

        _thumbnails.Requests.Clear();
        await viewModel.StartCommand.ExecuteAsync(null);

        var withCover = Assert.Single(_thumbnails.RequestsFor(first.InputPath));
        Assert.True(withCover.UsesCustomImage);
        Assert.Equal(coverPath, withCover.CustomImagePath);
        Assert.EndsWith(".THM", withCover.OutputPath);
        Assert.Equal(new VideoResolution(160, 120), withCover.Size);

        var withoutCover = Assert.Single(_thumbnails.RequestsFor(second.InputPath));
        Assert.False(withoutCover.UsesCustomImage);
        Assert.Null(withoutCover.CustomImagePath);
    }

    [AvaloniaFact]
    public async Task UntouchedItemsUseTheAutoFrameAtTenPercent()
    {
        var viewModel = CreateViewModel();
        var (first, _) = await AddTwoItemsAsync(viewModel);

        _thumbnails.Requests.Clear();
        await viewModel.StartCommand.ExecuteAsync(null);

        var request = Assert.Single(_thumbnails.RequestsFor(first.InputPath));

        Assert.False(request.UsesCustomImage);
        Assert.Equal(TimeSpan.FromMinutes(2) * 0.10, request.FramePosition);
    }

    [AvaloniaFact]
    public async Task CustomCoverFlowsIntoThePlanHandedToTheConverter()
    {
        var viewModel = CreateViewModel();
        var (first, second) = await AddTwoItemsAsync(viewModel);

        var coverPath = CreateImage("cover-a.png");
        _dialogs.ImagesToReturn.Enqueue(coverPath);
        await viewModel.PickCoverCommand.ExecuteAsync(first);

        _converter.Plans.Clear();
        await viewModel.StartCommand.ExecuteAsync(null);

        var firstPlan = _converter.Plans.Single(plan => plan.InputPath == first.InputPath);
        var secondPlan = _converter.Plans.Single(plan => plan.InputPath == second.InputPath);

        Assert.Equal(coverPath, firstPlan.CustomCoverPath);
        Assert.Null(secondPlan.CustomCoverPath);
    }

    [AvaloniaFact]
    public async Task Ps3AndVitaDoNotOfferCoversAndProduceNoThumbnail()
    {
        var viewModel = CreateViewModel();
        var (first, _) = await AddTwoItemsAsync(viewModel);

        foreach (var device in new[] { TargetDevice.Ps3, TargetDevice.Vita })
        {
            viewModel.SelectedProfile = DeviceProfileCatalog.For(device);
            await viewModel.RefreshRealizedCoversAsync();

            Assert.False(first.SupportsCover);

            _thumbnails.Requests.Clear();
            await viewModel.StartCommand.ExecuteAsync(null);

            Assert.Empty(_thumbnails.Requests);
        }
    }

    [AvaloniaFact]
    public async Task CancellingThePickerLeavesTheExistingCoverAlone()
    {
        var viewModel = CreateViewModel();
        var (first, _) = await AddTwoItemsAsync(viewModel);

        await viewModel.PickCoverCommand.ExecuteAsync(first);

        Assert.Equal(1, _dialogs.ImagePickerCalls);
        Assert.False(first.HasCustomCover);
    }

    private MainWindowViewModel CreateViewModel()
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
        return viewModel;
    }

    private async Task<(QueueItemViewModel First, QueueItemViewModel Second)> AddTwoItemsAsync(
        MainWindowViewModel viewModel)
    {
        var first = CreateVideo("first.mkv");
        var second = CreateVideo("second.mkv");

        await viewModel.EnqueueAsync([first, second]);

        Assert.Equal(2, viewModel.Queue.Count);
        Assert.All(viewModel.Queue, item => Assert.True(item.SupportsCover));

        return (viewModel.Queue[0], viewModel.Queue[1]);
    }

    private string CreateVideo(string name)
    {
        var path = Path.Combine(_workspace, name);
        File.WriteAllText(path, "stub video");
        return path;
    }

    private string CreateImage(string name)
    {
        var path = Path.Combine(_workspace, name);
        File.WriteAllText(path, "stub image");
        return path;
    }
}
