using Avalonia.Headless.XUnit;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Tests.Fakes;
using ConsoleForge.UI.Models;
using ConsoleForge.UI.ViewModels;

namespace ConsoleForge.Tests;

public class RowControlTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-rows-" + Guid.NewGuid().ToString("N"));

    private readonly RecordingConverter _converter = new();
    private readonly RecordingThumbnailService _thumbnails = new();
    private readonly ScriptedDialogService _dialogs = new();

    public RowControlTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public async Task CancellingAQueuedRowWithConfirmationRemovesOnlyThatRow()
    {
        var viewModel = await CreateQueueAsync(3);
        var target = viewModel.Queue[1];
        var survivor = viewModel.Queue[2];

        _dialogs.ConfirmAnswers.Enqueue(true);
        await viewModel.CancelItemCommand.ExecuteAsync(target);

        Assert.DoesNotContain(target, viewModel.Queue);
        Assert.Contains(survivor, viewModel.Queue);
        Assert.Equal(2, viewModel.Queue.Count);
    }

    [AvaloniaFact]
    public async Task DecliningTheConfirmationChangesNothing()
    {
        var viewModel = await CreateQueueAsync(3);
        var target = viewModel.Queue[1];

        _dialogs.ConfirmAnswers.Enqueue(false);
        await viewModel.CancelItemCommand.ExecuteAsync(target);

        Assert.Equal(3, viewModel.Queue.Count);
        Assert.Contains(target, viewModel.Queue);
        Assert.Equal(QueueItemStatus.Ready, target.Status);
        Assert.Single(_dialogs.ConfirmPrompts);
    }

    [AvaloniaFact]
    public async Task CancellingTheEncodingRowKillsThatConversionAndTheQueueContinues()
    {
        var viewModel = await CreateQueueAsync(3);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);

        var active = viewModel.Queue[0];
        Assert.Equal(QueueItemStatus.Converting, active.Status);
        var activeOutput = _converter.InFlight!.OutputPath;

        _converter.BlockUntil = null;
        _dialogs.ConfirmAnswers.Enqueue(true);
        await viewModel.CancelItemCommand.ExecuteAsync(active);
        gate.TrySetCanceled();

        await run;

        Assert.Equal(QueueItemStatus.Cancelled, active.Status);
        Assert.Contains(activeOutput, _converter.KilledOutputs);
        Assert.False(File.Exists(activeOutput));

        Assert.Equal(QueueItemStatus.Completed, viewModel.Queue[1].Status);
        Assert.Equal(QueueItemStatus.Completed, viewModel.Queue[2].Status);
    }

    [AvaloniaFact]
    public async Task PausingTheActiveRowMarksItPausedAndLetsTheQueueMoveOn()
    {
        var viewModel = await CreateQueueAsync(3);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);

        var active = viewModel.Queue[0];

        _converter.BlockUntil = null;
        viewModel.PauseItemCommand.Execute(active);
        gate.TrySetCanceled();

        await run;

        Assert.Equal(QueueItemStatus.Paused, active.Status);
        Assert.Equal(QueueItemStatus.Completed, viewModel.Queue[1].Status);
        Assert.Equal(QueueItemStatus.Completed, viewModel.Queue[2].Status);
        Assert.Contains(active, viewModel.Queue);
        Assert.Equal(0, active.Percent);
    }

    [AvaloniaFact]
    public async Task PausingNeverAsksForConfirmation()
    {
        var viewModel = await CreateQueueAsync(2);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);

        _converter.BlockUntil = null;
        viewModel.PauseItemCommand.Execute(viewModel.Queue[0]);
        gate.TrySetCanceled();
        await run;

        Assert.Empty(_dialogs.ConfirmPrompts);
    }

    [AvaloniaFact]
    public async Task ResumingAPausedRowRequeuesItForAFullReEncode()
    {
        var viewModel = await CreateQueueAsync(2);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);

        var active = viewModel.Queue[0];
        _converter.BlockUntil = null;
        viewModel.PauseItemCommand.Execute(active);
        gate.TrySetCanceled();
        await run;

        Assert.Equal(QueueItemStatus.Paused, active.Status);

        _converter.Plans.Clear();
        await viewModel.ResumeItemCommand.ExecuteAsync(active);

        Assert.Equal(QueueItemStatus.Completed, active.Status);
        Assert.Contains(_converter.Plans, plan => plan.InputPath == active.InputPath);
    }

    [AvaloniaFact]
    public async Task PausingNeverEndsUpInFailedEvenWhenTheConverterThrowsSomethingElse()
    {
        var viewModel = await CreateQueueAsync(2);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;
        _converter.ThrowOnCancel = new InvalidOperationException("ffmpeg exited with code 255.");

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);

        var active = viewModel.Queue[0];
        _converter.BlockUntil = null;
        viewModel.PauseItemCommand.Execute(active);
        gate.TrySetCanceled();
        await run;

        Assert.Equal(QueueItemStatus.Paused, active.Status);
        Assert.NotEqual(QueueItemStatus.Failed, active.Status);
        Assert.Null(active.ErrorMessage);
    }

    [AvaloniaFact]
    public async Task StoppingNeverEndsUpInFailedEvenWhenTheConverterThrowsSomethingElse()
    {
        var viewModel = await CreateQueueAsync(2);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;
        _converter.ThrowOnCancel = new InvalidOperationException("ffmpeg exited with code 255.");

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);

        var active = viewModel.Queue[0];
        _converter.BlockUntil = null;
        _dialogs.ConfirmAnswers.Enqueue(true);
        await viewModel.CancelItemCommand.ExecuteAsync(active);
        gate.TrySetCanceled();
        await run;

        Assert.Equal(QueueItemStatus.Cancelled, active.Status);
    }

    [AvaloniaFact]
    public async Task AGenuineFailureIsStillReportedAsFailed()
    {
        var viewModel = await CreateQueueAsync(2);
        _converter.ThrowAlways = new InvalidOperationException("ffmpeg exited with code 1.");

        await viewModel.StartCommand.ExecuteAsync(null);

        Assert.All(viewModel.Queue, item => Assert.Equal(QueueItemStatus.Failed, item.Status));
        Assert.All(viewModel.Queue, item => Assert.NotNull(item.ErrorMessage));
    }

    [AvaloniaFact]
    public async Task ResumeTakesAPausedRowBackThroughEncodingToCompleted()
    {
        var viewModel = await CreateQueueAsync(2);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);

        var active = viewModel.Queue[0];
        _converter.BlockUntil = null;
        viewModel.PauseItemCommand.Execute(active);
        gate.TrySetCanceled();
        await run;

        Assert.Equal(QueueItemStatus.Paused, active.Status);

        _converter.Plans.Clear();
        await viewModel.ResumeItemCommand.ExecuteAsync(active);

        Assert.Equal(QueueItemStatus.Completed, active.Status);
        Assert.Contains(_converter.Plans, plan => plan.InputPath == active.InputPath);
        Assert.False(active.PauseRequested);
    }

    [AvaloniaFact]
    public async Task PausedRowsAreSkippedByARunUntilTheyAreResumed()
    {
        var viewModel = await CreateQueueAsync(2);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);

        var paused = viewModel.Queue[0];
        _converter.BlockUntil = null;
        viewModel.PauseItemCommand.Execute(paused);
        gate.TrySetCanceled();
        await run;

        _converter.Plans.Clear();
        await viewModel.StartCommand.ExecuteAsync(null);

        Assert.Equal(QueueItemStatus.Paused, paused.Status);
        Assert.DoesNotContain(_converter.Plans, plan => plan.InputPath == paused.InputPath);
    }

    [AvaloniaFact]
    public async Task RowControlVisibilityFollowsStatus()
    {
        var viewModel = await CreateQueueAsync(1);
        var item = viewModel.Queue[0];

        Assert.True(item.CanCancel);
        Assert.False(item.CanPause);
        Assert.False(item.CanResume);

        item.Status = QueueItemStatus.Converting;
        Assert.True(item.CanPause);
        Assert.True(item.CanCancel);
        Assert.False(item.CanResume);

        item.Status = QueueItemStatus.Paused;
        Assert.True(item.CanResume);
        Assert.False(item.CanPause);

        item.Status = QueueItemStatus.Completed;
        Assert.False(item.CanCancel);
        Assert.False(item.CanPause);
        Assert.False(item.CanResume);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "condition was never met");
    }

    private async Task<MainWindowViewModel> CreateQueueAsync(int count)
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

        await viewModel.EnqueueAsync(Enumerable.Range(0, count).Select(index =>
        {
            var path = Path.Combine(_workspace, $"video_{index}.mkv");
            File.WriteAllText(path, "stub");
            return path;
        }));

        return viewModel;
    }
}
