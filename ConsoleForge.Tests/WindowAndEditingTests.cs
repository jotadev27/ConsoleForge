using Avalonia.Headless.XUnit;
using ConsoleForge.Core.Profiles;
using ConsoleForge.Core.Services;
using ConsoleForge.Tests.Fakes;
using ConsoleForge.UI.Models;
using ConsoleForge.UI.ViewModels;

namespace ConsoleForge.Tests;

public class WindowAndEditingTests : IDisposable
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), "consoleforge-window-" + Guid.NewGuid().ToString("N"));

    private readonly RecordingConverter _converter = new();
    private readonly RecordingThumbnailService _thumbnails = new();
    private readonly ScriptedDialogService _dialogs = new();

    public WindowAndEditingTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        GC.SuppressFinalize(this);
    }

    [AvaloniaFact]
    public async Task ExitingWithNoActiveConversionDoesNotPrompt()
    {
        var viewModel = await CreateQueueAsync(2);

        Assert.True(await viewModel.ConfirmExitAsync());
        Assert.Empty(_dialogs.ConfirmPrompts);
    }

    [AvaloniaFact]
    public async Task ExitingAfterEverythingFinishedDoesNotPrompt()
    {
        var viewModel = await CreateQueueAsync(2);
        await viewModel.StartCommand.ExecuteAsync(null);

        Assert.True(await viewModel.ConfirmExitAsync());
        Assert.Empty(_dialogs.ConfirmPrompts);
    }

    [AvaloniaFact]
    public async Task ExitingDuringAConversionPromptsAndCanBeDeclined()
    {
        var viewModel = await CreateQueueAsync(2);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);

        _dialogs.ConfirmAnswers.Enqueue(false);
        Assert.False(await viewModel.ConfirmExitAsync());
        Assert.Single(_dialogs.ConfirmPrompts);
        Assert.Equal(QueueItemStatus.Converting, viewModel.Queue[0].Status);

        _converter.BlockUntil = null;
        gate.TrySetResult();
        await run;
    }

    [AvaloniaFact]
    public async Task ConfirmingExitDuringAConversionStopsTheRun()
    {
        var viewModel = await CreateQueueAsync(3);
        var gate = new TaskCompletionSource();
        _converter.BlockUntil = gate;

        var run = viewModel.StartCommand.ExecuteAsync(null);
        await WaitForAsync(() => _converter.InFlight is not null);
        var activeOutput = _converter.InFlight!.OutputPath;

        _converter.BlockUntil = null;
        _dialogs.ConfirmAnswers.Enqueue(true);
        Assert.True(await viewModel.ConfirmExitAsync());
        gate.TrySetCanceled();

        await run;

        Assert.False(viewModel.IsRunning);
        Assert.False(File.Exists(activeOutput));
        Assert.DoesNotContain(viewModel.Queue, item => item.Status == QueueItemStatus.Completed);
    }

    [AvaloniaFact]
    public void VideoBitrateOptionsNeverLeaveTheActiveProfileRange()
    {
        var viewModel = CreateViewModel();

        foreach (var profile in DeviceProfileCatalog.All)
        {
            viewModel.SelectedProfile = profile;

            Assert.NotEmpty(viewModel.VideoBitrateOptions);
            Assert.All(viewModel.VideoBitrateOptions, value =>
                Assert.InRange(value, profile.MinVideoBitrateKbps, profile.MaxVideoBitrateKbps));

            Assert.NotEmpty(viewModel.AudioBitrateOptions);
            Assert.All(viewModel.AudioBitrateOptions, value =>
                Assert.InRange(value, profile.MinAudioBitrateKbps, profile.MaxAudioBitrateKbps));
        }
    }

    [AvaloniaFact]
    public void BitrateOptionsAlwaysContainTheProfileDefaultAndSelectIt()
    {
        var viewModel = CreateViewModel();

        foreach (var profile in DeviceProfileCatalog.All)
        {
            viewModel.SelectedProfile = profile;

            Assert.Contains(profile.DefaultVideoBitrateKbps, viewModel.VideoBitrateOptions);
            Assert.Contains(profile.DefaultAudioBitrateKbps, viewModel.AudioBitrateOptions);

            Assert.Equal(profile.DefaultVideoBitrateKbps, viewModel.VideoBitrateKbps);
            Assert.Equal(profile.DefaultAudioBitrateKbps, viewModel.AudioBitrateKbps);
        }
    }

    [AvaloniaFact]
    public void SwitchingProfileRebuildsTheBitrateOptionLists()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedProfile = DeviceProfileCatalog.Ps3;
        var ps3Options = viewModel.VideoBitrateOptions.ToList();

        viewModel.SelectedProfile = DeviceProfileCatalog.Psp;
        var pspOptions = viewModel.VideoBitrateOptions.ToList();

        Assert.NotEqual(ps3Options, pspOptions);
        Assert.DoesNotContain(ps3Options, value => pspOptions.Contains(value));
    }

    [AvaloniaFact]
    public async Task RenamingChangesTheOutputNameAndLeavesTheSourceAlone()
    {
        var viewModel = await CreateQueueAsync(2);
        var item = viewModel.Queue[0];
        var sourcePath = item.InputPath;

        item.BeginNameEdit();
        item.NameEditText = "My Renamed Movie";
        Assert.True(item.CommitNameEdit());

        await viewModel.StartCommand.ExecuteAsync(null);

        var plan = _converter.Plans.Single(candidate => candidate.InputPath == sourcePath);

        Assert.Equal("My_Renamed_Movie.mp4", Path.GetFileName(plan.OutputPath));
        Assert.Equal("My_Renamed_Movie.THM", Path.GetFileName(plan.ThumbnailPath!));
        Assert.True(File.Exists(sourcePath), "the source file must not be renamed or moved");
        Assert.Equal(sourcePath, item.InputPath);

        var untouched = _converter.Plans.Single(candidate => candidate.InputPath == viewModel.Queue[1].InputPath);
        Assert.Equal("video_1.mp4", Path.GetFileName(untouched.OutputPath));
    }

    [AvaloniaFact]
    public async Task EscapeAndFocusLossRevertTheRename()
    {
        var viewModel = await CreateQueueAsync(1);
        var item = viewModel.Queue[0];
        var original = item.DisplayName;

        item.BeginNameEdit();
        item.NameEditText = "Discarded";
        item.CancelNameEdit();

        Assert.False(item.IsEditingName);
        Assert.Null(item.OutputBaseName);
        Assert.Equal(original, item.DisplayName);
    }

    [AvaloniaTheory]
    [InlineData("bad/name")]
    [InlineData("bad\\name")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task InvalidNamesAreRejectedAndKeepTheEditorOpen(string candidate)
    {
        var viewModel = await CreateQueueAsync(1);
        var item = viewModel.Queue[0];

        item.BeginNameEdit();
        item.NameEditText = candidate;

        Assert.False(item.CommitNameEdit());
        Assert.True(item.IsEditingName);
        Assert.Null(item.OutputBaseName);
    }

    [AvaloniaFact]
    public async Task RenamingOntoAnExistingNameDoesNotOverwrite()
    {
        var viewModel = await CreateQueueAsync(2);

        foreach (var item in viewModel.Queue)
        {
            item.BeginNameEdit();
            item.NameEditText = "Same Name";
            Assert.True(item.CommitNameEdit());
        }

        await viewModel.StartCommand.ExecuteAsync(null);

        var outputs = _converter.Plans.Select(plan => plan.OutputPath).ToList();

        Assert.Equal(2, outputs.Distinct().Count());
        Assert.Contains(outputs, path => Path.GetFileName(path) == "Same_Name.mp4");
        Assert.Contains(outputs, path => Path.GetFileName(path) == "Same_Name_2.mp4");
    }

    [Fact]
    public void FileNameValidationRejectsOsInvalidCharacters()
    {
        Assert.True(OutputPathResolver.IsValidFileName("Perfectly Fine 01"));
        Assert.False(OutputPathResolver.IsValidFileName("with/slash"));
        Assert.False(OutputPathResolver.IsValidFileName("with\\backslash"));
        Assert.False(OutputPathResolver.IsValidFileName(null));
        Assert.False(OutputPathResolver.IsValidFileName(" leading"));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "condition was never met");
    }

    private MainWindowViewModel CreateViewModel() =>
        new(
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

    private async Task<MainWindowViewModel> CreateQueueAsync(int count)
    {
        var viewModel = CreateViewModel();
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
