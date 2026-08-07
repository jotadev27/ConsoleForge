using System.Diagnostics;
using System.Text;

namespace ConsoleForge.Infrastructure.FFmpeg;

internal static class ProcessRunner
{
    public static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(executable, arguments);
        using var process = new Process { StartInfo = startInfo };

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.Start();

        var outputTask = ReadAllAsync(process.StandardOutput, standardOutput, cancellationToken);
        var errorTask = ReadAllAsync(process.StandardError, standardError, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);

        return (process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    public static ProcessStartInfo CreateStartInfo(string executable, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
        catch (SystemException)
        {
        }
    }

    private static async Task ReadAllAsync(StreamReader reader, StringBuilder sink, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new char[4096];
            int read;
            while ((read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                sink.Append(buffer, 0, read);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }
}
