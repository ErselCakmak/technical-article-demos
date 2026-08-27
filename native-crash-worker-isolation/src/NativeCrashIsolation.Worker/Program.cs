using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using NativeCrashIsolation.Protocol;

if (!TryReadArguments(args, out string pipeName, out string expectedToken))
{
    Console.Error.WriteLine("Usage: NativeCrashIsolation.Worker --pipe <name> --token <token>");
    return 64;
}

try
{
    await using var pipe = new NamedPipeClientStream(
        serverName: ".",
        pipeName,
        PipeDirection.InOut,
        PipeOptions.Asynchronous);

    using var connectionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await pipe.ConnectAsync(connectionTimeout.Token);

    WorkerRequest request = await PipeProtocol.ReadRequestAsync(pipe, CancellationToken.None);
    if (!TokensMatch(expectedToken, request.Token))
    {
        await PipeProtocol.WriteResponseAsync(
            pipe,
            new WorkerResponse(false, "Authentication token mismatch.", Array.Empty<byte>()),
            CancellationToken.None);
        return 10;
    }

    switch (request.Operation)
    {
        case WorkerOperation.Success:
            // A real worker would enter its native library boundary here.
            Array.Reverse(request.Payload);
            await PipeProtocol.WriteResponseAsync(
                pipe,
                new WorkerResponse(true, "Worker completed the operation.", request.Payload),
                CancellationToken.None);
            return 0;

        case WorkerOperation.Fail:
            await PipeProtocol.WriteResponseAsync(
                pipe,
                new WorkerResponse(false, "The operation failed without corrupting the process.", Array.Empty<byte>()),
                CancellationToken.None);
            return 2;

        case WorkerOperation.Crash:
            // A real access violation would also close the pipe before a response.
            // Exit explicitly so this public demo stays deterministic and dump-free.
            Environment.Exit(70);
            return 70; // Unreachable, required for flow analysis.

        case WorkerOperation.Hang:
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 71;

        default:
            return 65;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static bool TryReadArguments(string[] arguments, out string pipeName, out string token)
{
    pipeName = string.Empty;
    token = string.Empty;

    if (arguments.Length != 4 ||
        !string.Equals(arguments[0], "--pipe", StringComparison.Ordinal) ||
        !string.Equals(arguments[2], "--token", StringComparison.Ordinal))
    {
        return false;
    }

    pipeName = arguments[1];
    token = arguments[3];
    return !string.IsNullOrWhiteSpace(pipeName) && !string.IsNullOrWhiteSpace(token);
}

static bool TokensMatch(string expected, string actual)
{
    byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
    byte[] actualBytes = Encoding.UTF8.GetBytes(actual);
    return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
}
