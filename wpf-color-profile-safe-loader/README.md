# WPF Color Profile Safe Loader

A public-safe teaching project for handling an environment-specific WPF image
decode failure without disabling color management for every image.

The companion article is:

https://erselcakmak.com/articles/when-a-png-crashes-a-wpf-application

## The Failure Boundary

WPF can throw an `ArithmeticException` / `OverflowException` while completing a
`BitmapImage` whose PNG contains an embedded ICC (`iCCP`) profile. The observed
failure depends on the Windows color environment and may not reproduce on a
healthy development machine.

Microsoft tracks the matching report here:

https://github.com/dotnet/wpf/issues/3884

`BitmapCreateOptions.IgnoreColorProfile` is documented here:

https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapcreateoptions?view=windowsdesktop-10.0

## What This Demo Shows

- Normal color-managed decoding remains the first attempt.
- Only an arithmetic color-conversion failure triggers a retry.
- The retry opens a fresh file, stream, byte array, or URI source.
- The fallback adds `BitmapCreateOptions.IgnoreColorProfile` and emits a
  diagnostic event.
- Missing files, unsupported images, permission failures, and other exceptions
  are not converted into false success.
- Direct XAML image sources need their own explicit policy because they can decode
  before application code reaches a C# helper.

## Projects

```text
src/
  WpfColorProfileSafeLoader/       reusable loader and fallback policy
  WpfColorProfileSafeLoader.Demo/  small WPF comparison UI
tests/
  WpfColorProfileSafeLoader.Tests/ deterministic policy tests
scripts/
  verify.ps1                       one-command build and test verification
```

## Run the Demo

Requirements:

- Windows
- .NET 8 SDK

```powershell
dotnet run --project src/WpfColorProfileSafeLoader.Demo/WpfColorProfileSafeLoader.Demo.csproj
```

Choose a local PNG and compare normal loading with safe loading. On an unaffected
machine both paths should succeed normally. On an affected color-profile setup,
the safe path can recover and display a fallback message.

## Verify

```powershell
.\scripts\verify.ps1
```

The tests simulate the conversion exception instead of pretending the Windows
defect is portable. They verify the behavior controlled by the application:

- no retry after a successful decode;
- one retry with `IgnoreColorProfile` after an arithmetic failure;
- diagnostic notification on fallback;
- unrelated exceptions propagate;
- an already ignored profile does not enter a retry loop.

## XAML Boundary

Decorative UI artwork that does not require color correction can opt out at the
declarative boundary:

```xml
<Window.Resources>
    <BitmapImage
        x:Key="SafeDiagram"
        UriSource="/Assets/layout-diagram.png"
        CreateOptions="IgnoreColorProfile" />
</Window.Resources>
```

Do not apply that policy blindly to rendered output, material previews, photos,
or other color-critical content. Route those images through the normal-first
loader instead.

## Important Limitation

This repository does not include customer images, display profiles, crash dumps,
or production application code. The real operating-system failure requires a
specific combination of an embedded image profile and an affected Windows display
profile. Do not change a calibrated production display merely to force the crash;
use an isolated test machine or VM if exact reproduction is necessary.
