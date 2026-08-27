$ErrorActionPreference = "Stop"

$demoRoot = Split-Path -Parent $PSScriptRoot
$hostProject = Join-Path $demoRoot "src\NativeCrashIsolation.Host\NativeCrashIsolation.Host.csproj"
$hostDll = Join-Path $demoRoot "src\NativeCrashIsolation.Host\bin\Release\net8.0\NativeCrashIsolation.Host.dll"

dotnet build $hostProject --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

$previousTimeout = $env:NATIVE_CRASH_DEMO_TIMEOUT_SECONDS
$env:NATIVE_CRASH_DEMO_TIMEOUT_SECONDS = "1"

try {
    foreach ($scenario in @("success", "fail", "crash", "hang")) {
        Write-Host "`nScenario: $scenario"
        $output = & dotnet $hostDll $scenario 2>&1 | Out-String
        Write-Host $output.Trim()

        if ($LASTEXITCODE -ne 0) {
            throw "Host returned exit code $LASTEXITCODE for '$scenario'."
        }

        if ($output -notmatch "Host process is still running\.") {
            throw "Host survival marker was missing for '$scenario'."
        }

        $expectedOutcome = switch ($scenario) {
            "success" { "Completed" }
            "fail" { "Rejected" }
            "crash" { "Crashed" }
            "hang" { "TimedOut" }
        }

        if ($output -notmatch "Outcome: $expectedOutcome") {
            throw "Expected '$expectedOutcome' for '$scenario'."
        }
    }
}
finally {
    $env:NATIVE_CRASH_DEMO_TIMEOUT_SECONDS = $previousTimeout
}

Write-Host "`nAll containment scenarios passed."
