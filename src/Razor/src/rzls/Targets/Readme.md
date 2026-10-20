### Note

This (and code in Rules) was pulled from the [sdk](https://github.com/dotnet/sdk/blob/8f983a2c12162e49af7a7f2eb7744d220859f42b/src/RazorSdk/Targets/Microsoft.NET.Sdk.Razor.DesignTime.targets). These files are for tooling to define specific design time behavior and thus should be owned by tooling. As of writing these are only used in VS Code.

Visual Studio uses the preserved assets in `src/Shared/files/RazorDesignTime` instead.
Those files came from `Microsoft.NET.Sdk.Razor` version `6.0.0-alpha.1.21072.5`,
built from [aspnetcore commit 20b6779cf3af2fed6a8fe64a0865cfab50b776bb](https://github.com/dotnet/aspnetcore/tree/20b6779cf3af2fed6a8fe64a0865cfab50b776bb),
under Apache-2.0 (see `NOTICE.txt`). Keeping only the targets and five XAML rules
avoids restoring the package's unused compiler and runtime assemblies.

The variants are intentionally separate: the VS Code rules hide the extension assembly
and Razor configuration properties, and its targets include an editor-config workaround.
Visual Studio retains the package's original property visibility and target behaviour.