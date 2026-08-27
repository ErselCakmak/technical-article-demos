using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using NativeCrashIsolation.Protocol;

namespace NativeCrashIsolation.Host;

internal enum WorkerOutcome
{
    Completed,
    Rejected,
    Crashed,
    TimedOut,
}

internal sealed record WorkerExecutionResult(
    WorkerOutcome Outcome,
    string Message,
    byte[] Payload);

internal static class WorkerClient
{
    public static async Task<WorkerExecutionResult> ExecuteAsync(
        WorkerOperation operation,
        byte[] payload,
        TimeSpan timeout)
    {
        string pipeName = $"native-crash-isolation-{Guid.NewGuid():N}";
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        using Process worker = StartWorker(pipeName, token);
        Task<string> standardErrorTask = worker.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(timeout);

        try
        {
            await pipe.WaitForConnectionAsync(deadline.Token);
            await PipeProtocol.WriteRequestAsync(
                pipe,
                new WorkerRequest(token, operation, payload),
                deadline.Token);

            WorkerResponse response = await PipeProtocol.ReadResponseAsync(pipe, deadline.Token);
            await worker.WaitForExitAsync(deadline.Token);

            return response.Success
                ? new WorkerExecutionResult(WorkerOutcome.Completed, response.Message, response.Payload)
                : new WorkerExecutionResult(WorkerOutcome.Rejected, response.Message, response.Payload);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            TryKill(worker);
            return new WorkerExecutionResult(
                WorkerOutcome.TimedOut,
                $"Worker exceeded the {timeout.TotalSeconds:0.##}-second deadline and was terminated.",
                Array.Empty<byte>());
        }
        catch (EndOfStreamException)
        {
            await WaitBrieflyForExitAsync(worker);
            string diagnostics = await ReadDiagnosticsAsync(standardErrorTask);
            return new WorkerExecutionResult(
                WorkerOutcome.Crashed,
                DescribeCrash(worker, diagnostics),
                Array.Empty<byte>());
        }
        catch (IOException)
        {
            await WaitBrieflyForExitAsync(worker);
            string diagnostics = await ReadDiagnosticsAsync(standardErrorTask);
            return new WorkerExecutionResult(
                WorkerOutcome.Crashed,
                DescribeCrash(worker, diagnostics),
                Array.Empty<byte>());
        }
        finally
        {
            if (!worker.HasExited)
            {
                TryKill(worker);
            }
        }
    }

    private static Process StartWorker(string pipeName, string token)
    {
        string workerPath = Path.Combine(
            AppContext.BaseDirectory,
            "worker",
            "NativeCrashIsolation.Worker.dll");

        if (!File.Exists(workerPath))
        {
            throw new FileNotFoundException(
                "Worker artifacts are missing. Build NativeCrashIsolation.Host before running the demo.",
                workerPath);
        }

        string dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var startInfo = new ProcessStartInfo
        {
            FileName = dotnetHost,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        startInfo.ArgumentList.Add(workerPath);
        startInfo.ArgumentList.Add("--pipe");
        startInfo.ArgumentList.Add(pipeName);
        startInfo.ArgumentList.Add("--token");
        startInfo.ArgumentList.Add(token);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("The worker process could not be started.");
    }

    private static string DescribeCrash(Process worker, string diagnostics)
    {
        string exitCode = worker.HasExited ? worker.ExitCode.ToString() : "unknown";
        return string.IsNullOrWhiteSpace(diagnostics)
            ? $"Worker disconnected before returning a response. Exit code: {exitCode}."
            : $"Worker disconnected before returning a response. Exit code: {exitCode}. Diagnostic: {diagnostics}";
    }

    private static async Task WaitBrieflyForExitAsync(Process worker)
    {
        if (worker.HasExited)
        {
            return;
        }

        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try
        {
            await worker.WaitForExitAsync(wait.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(worker);
        }
    }

    private static async Task<string> ReadDiagnosticsAsync(Task<string> diagnosticsTask)
    {
        if (!diagnosticsTask.IsCompleted)
        {
            return string.Empty;
        }

        string diagnostics = await diagnosticsTask;
        const int maximumLength = 300;
        return diagnostics.Length <= maximumLength
            ? diagnostics.Trim()
            : diagnostics[..maximumLength].Trim() + "...";
    }

    private static void TryKill(Process worker)
    {
        try
        {
            if (!worker.HasExited)
            {
                worker.Kill(entireProcessTree: true);
                worker.WaitForExit();
            }
        }
        catch
        {
            // Cleanup must not replace the original worker outcome.
        }
    }
}
