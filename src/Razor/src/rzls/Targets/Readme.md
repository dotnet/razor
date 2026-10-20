### Note

This (and code in Rules) was pulled from the [sdk](https://github.com/dotnet/sdk/blob/8f983a2c12162e49af7a7f2eb7744d220859f42b/src/RazorSdk/Targets/Microsoft.NET.Sdk.Razor.DesignTime.targets). These files are for tooling to define specific design time behavior and thus should be owned by tooling. As of writing these are only used in VS Code.

### Visual Studio assets

Visual Studio uses the six original text assets in `src/Shared/files/RazorDesignTime`
instead. They replace the files previously read from `Microsoft.NET.Sdk.Razor`
6.0.0-alpha.1.21072.5, without restoring its obsolete tools and bundled assemblies.
Their source is [dotnet/aspnetcore at commit 20b6779cf3af2fed6a8fe64a0865cfab50b776bb](https://github.com/dotnet/aspnetcore/tree/20b6779cf3af2fed6a8fe64a0865cfab50b776bb),
as recorded in that package. The scoped `cgmanifest.json` lists the copied files.
They retain their Apache-2.0 licence and original copyright notices; see `NOTICE.txt`.

Keep those files byte-for-byte identical to the package, including whitespace,
newlines and BOMs: desktop rule generation uses the XAML bytes when naming its
fallback classes. The VSIX paths, embedded rule names and generated property
contracts must also stay unchanged.

Do not substitute these VS Code assets for the Visual Studio copies. These rules
hide four properties exposed by the original package, and the targets include a
VS Code-specific design-time workaround.

The test apps use the serviced SDK selected by the repository rather than
importing the obsolete package's build tools. Their target frameworks are unchanged.