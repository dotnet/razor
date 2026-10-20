# Visual Studio Razor design-time assets

`src\Shared\files\RazorDesignTime` contains the six text-only assets used by the Visual Studio Razor extension. They are copied byte-for-byte from the `Microsoft.NET.Sdk.Razor` source at the commit recorded in the adjacent `cgmanifest.json`. The source is licensed under Apache-2.0; see `NOTICE.txt`.

Keep these assets separate from the Razor VS Code extension grammars and configurations. Those files serve different hosts and are not replacements for the Visual Studio design-time project rules or targets. Do not reformat the XAML: desktop MSBuild derives fallback rule names from its bytes.
