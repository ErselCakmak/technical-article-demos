$ErrorActionPreference = "Stop"

$demoRoot = Split-Path -Parent $PSScriptRoot
$demoProject = Join-Path $demoRoot "src\WpfColorProfileSafeLoader.Demo\WpfColorProfileSafeLoader.Demo.csproj"
$testProject = Join-Path $demoRoot "tests\WpfColorProfileSafeLoader.Tests\WpfColorProfileSafeLoader.Tests.csproj"

dotnet build $demoProject --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Demo build failed."
}

dotnet test $testProject --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Policy tests failed."
}

Write-Host "`nWPF color-profile fallback demo verified."
