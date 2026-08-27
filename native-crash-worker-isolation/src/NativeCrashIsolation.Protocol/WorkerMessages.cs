namespace NativeCrashIsolation.Protocol;

public enum WorkerOperation : byte
{
    Success = 1,
    Fail = 2,
    Crash = 3,
    Hang = 4,
}

public sealed record WorkerRequest(
    string Token,
    WorkerOperation Operation,
    byte[] Payload);

public sealed record WorkerResponse(
    bool Success,
    string Message,
    byte[] Payload);
