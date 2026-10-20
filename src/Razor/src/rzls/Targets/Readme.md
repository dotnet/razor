### Note

This (and code in Rules) was pulled from the [sdk](https://github.com/dotnet/sdk/blob/8f983a2c12162e49af7a7f2eb7744d220859f42b/src/RazorSdk/Targets/Microsoft.NET.Sdk.Razor.DesignTime.targets). These files are for tooling to define specific design time behavior and thus should be owned by tooling. As of writing these are only used in VS Code.

Visual Studio uses the separate copies in `src/Shared/files/RazorDesignTime`, retained from
`Microsoft.NET.Sdk.Razor` 6.0.0-alpha.1.21072.5 so the obsolete SDK tools no longer need to
be restored. Keep these variants separate: the VS Code rules hide properties exposed by
Visual Studio, and its targets contain an additional design-time editorconfig workaround.
The shared folder's `cgmanifest.json` records the six copied text files and their source;
`NOTICE.txt` records their Apache-2.0 attribution.