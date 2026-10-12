using System.Diagnostics;
using System.Text;

namespace CapaOptimizer.Core;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
public sealed class ProcessExecutedEventArgs(string fileName, string arguments, ProcessResult result) : EventArgs
{
    public string FileName { get; } = fileName;
    public string Arguments { get; } = arguments;
    public ProcessResult Result { get; } = result;
}

public sealed class ProcessRunner
{
    public event EventHandler<ProcessExecutedEventArgs>? ProcessExecuted;

    public Task<ProcessResult> RunCmdAsync(string arguments, CancellationToken cancellationToken = default) =>
        RunAsync("cmd.exe", $"/c {arguments}", cancellationToken);

    public Task<ProcessResult> RunPowerShellAsync(string script, CancellationToken cancellationToken = default)
    {
        var safeScript = "$ProgressPreference = 'SilentlyContinue'; " + script;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(safeScript));
        return RunAsync("powershell.exe", $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", cancellationToken);
    }

    private async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        if (!process.Start())
            throw new InvalidOperationException($"Process başlatılamadı: {fileName}");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var result = new ProcessResult(process.ExitCode, await outputTask, await errorTask);
        ProcessExecuted?.Invoke(null, new ProcessExecutedEventArgs(fileName, arguments, result));
        return result;
    }
}
