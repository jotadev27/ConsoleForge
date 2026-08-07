using System.Diagnostics;
using ConsoleForge.Core.Models;
using ConsoleForge.Core.Services;

namespace ConsoleForge.Infrastructure.FFmpeg;

public sealed class FFmpegVideoConverter : IVideoConverter
{
    private const int RetainedErrorLines = 40;

    private readonly FFmpegLocator _locator;

    public FFmpegVideoConverter(FFmpegLocator locator) => _locator = locator;

    public async Task ConvertAsync(
        ConversionPlan plan,
        IProgress<ConversionProgress>? progress,
        Action<string>? log,
        CancellationToken cancellationToken = default)
    {
        var executable = _locator.RequireFFmpeg();
        var arguments = FFmpegArgumentBuilder.Build(plan);

        var outputDirectory = Path.GetDirectoryName(plan.OutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        log?.Invoke(FFmpegArgumentBuilder.ToDisplayString(executable, arguments));

        using var process = new Process
        {
            StartInfo = ProcessRunner.CreateStartInfo(executable, arguments)
        };

        var parser = new FFmpegProgressParser(plan.SourceDuration);
        var recentErrors = new Queue<string>(RetainedErrorLines);

        process.Start();

        var progressTask = PumpProgressAsync(process, parser, progress, cancellationToken);
        var logTask = PumpLogAsync(process, recentErrors, log, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            ProcessRunner.TryKill(process);
        }

        await DrainAsync(progressTask, logTask).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            ProcessRunner.TryKill(process);
            DeletePartialOutput(plan.OutputPath, log);
            throw new OperationCanceledException(cancellationToken);
        }

        if (process.ExitCode != 0)
        {
            DeletePartialOutput(plan.OutputPath, log);
            var detail = recentErrors.Count > 0
                ? Environment.NewLine + string.Join(Environment.NewLine, recentErrors)
                : string.Empty;
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}.{detail}");
        }

        if (!File.Exists(plan.OutputPath) || new FileInfo(plan.OutputPath).Length == 0)
        {
            throw new InvalidOperationException("ffmpeg reported success but produced no output file.");
        }
    }

    private static async Task DrainAsync(params Task[] pumps)
    {
        foreach (var pump in pumps)
        {
            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }

    private static async Task PumpProgressAsync(
        Process process,
        FFmpegProgressParser parser,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (parser.TryConsume(line, out var snapshot))
                {
                    progress?.Report(snapshot);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static async Task PumpLogAsync(
        Process process,
        Queue<string> recentErrors,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (recentErrors.Count == RetainedErrorLines) recentErrors.Dequeue();
                recentErrors.Enqueue(line);
                log?.Invoke(line);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static void DeletePartialOutput(string outputPath, Action<string>? log)
    {
        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
        catch (IOException exception)
        {
            log?.Invoke($"Could not remove partial output: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            log?.Invoke($"Could not remove partial output: {exception.Message}");
        }
    }
}
