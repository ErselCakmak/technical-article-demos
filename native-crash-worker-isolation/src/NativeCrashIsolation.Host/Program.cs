using System.Text;
using NativeCrashIsolation.Host;
using NativeCrashIsolation.Protocol;

if (args.Length != 1 || !TryParseOperation(args[0], out WorkerOperation operation))
{
    Console.Error.WriteLine("Usage: NativeCrashIsolation.Host <success|fail|crash|hang>");
    return 64;
}

TimeSpan timeout = ReadTimeout();
byte[] payload = Encoding.UTF8.GetBytes("sample geometry payload");

Console.WriteLine($"Host PID: {Environment.ProcessId}");
Console.WriteLine($"Starting one-shot worker for '{args[0]}' with a {timeout.TotalSeconds:0.##}s timeout.");

WorkerExecutionResult result = await WorkerClient.ExecuteAsync(operation, payload, timeout);

Console.WriteLine($"Outcome: {result.Outcome}");
Console.WriteLine($"Detail: {result.Message}");
if (result.Payload.Length > 0)
{
    Console.WriteLine($"Payload: {Encoding.UTF8.GetString(result.Payload)}");
}

Console.WriteLine("Host process is still running.");
return 0;

static bool TryParseOperation(string value, out WorkerOperation operation)
{
    operation = value.ToLowerInvariant() switch
    {
        "success" => WorkerOperation.Success,
        "fail" => WorkerOperation.Fail,
        "crash" => WorkerOperation.Crash,
        "hang" => WorkerOperation.Hang,
        _ => default,
    };

    return operation != default;
}

static TimeSpan ReadTimeout()
{
    const int defaultSeconds = 3;
    string? configured = Environment.GetEnvironmentVariable("NATIVE_CRASH_DEMO_TIMEOUT_SECONDS");

    return int.TryParse(configured, out int seconds) && seconds is >= 1 and <= 60
        ? TimeSpan.FromSeconds(seconds)
        : TimeSpan.FromSeconds(defaultSeconds);
}
