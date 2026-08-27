# Native Crash Worker Isolation Demo

Public-safe companion project for:

https://erselcakmak.com/articles/isolating-native-crashes-with-a-worker-process

This demo shows why an operating-system process is the useful fault boundary when a .NET desktop application calls native code that can corrupt memory, terminate unexpectedly, or hang.

The sample contains no production CAD code and no Open CASCADE dependency. Instead, the worker exposes four deterministic operations:

- `success` returns a transformed payload.
- `fail` returns a structured operation error.
- `crash` exits the worker with a non-zero code before a response is written, deterministically simulating a fatal native termination.
- `hang` stops responding until the host timeout kills the worker.

For every operation, the host remains alive and reports the outcome.

## Architecture

```text
NativeCrashIsolation.Host
    |
    |  versioned, length-prefixed Named Pipe message
    v
NativeCrashIsolation.Worker
    |
    +-- native library boundary would live here
```

The host creates a unique pipe and authentication token for every request. The worker handles one request and exits, so a damaged native heap is never reused by the next operation.

## Requirements

- .NET 8 SDK or later
- Windows, Linux, or macOS

## Run

Build the host project. Its project reference also builds and copies the worker artifacts:

```bash
dotnet build src/NativeCrashIsolation.Host
```

Then try each outcome:

```bash
dotnet run --project src/NativeCrashIsolation.Host -- success
dotnet run --project src/NativeCrashIsolation.Host -- fail
dotnet run --project src/NativeCrashIsolation.Host -- crash
dotnet run --project src/NativeCrashIsolation.Host -- hang
```

The `crash` and `hang` commands intentionally terminate the disposable worker. They do not terminate the host.

## Verify all scenarios

On PowerShell:

```powershell
.\scripts\verify.ps1
```

The timeout defaults to three seconds. Override it when experimenting:

```powershell
$env:NATIVE_CRASH_DEMO_TIMEOUT_SECONDS = "10"
```

## What this demo deliberately leaves out

- Real native or CAD dependencies
- Product-specific geometry serialization
- Worker pooling
- Retry policies
- Production telemetry and crash-dump collection

Those choices keep the repository focused on the containment boundary rather than a specific geometry kernel.
