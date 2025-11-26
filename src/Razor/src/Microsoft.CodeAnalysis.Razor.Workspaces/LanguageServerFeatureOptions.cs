// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal abstract class LanguageServerFeatureOptions
{
    public abstract bool SupportsFileManipulation { get; }

    public abstract string CSharpVirtualDocumentSuffix { get; }

    public abstract bool SingleServerSupport { get; }

    public abstract bool ShowAllCSharpCodeActions { get; }

    // Code action and rename paths in Windows VS Code need to be prefixed with '/':
    // https://github.com/dotnet/razor/issues/8131
    public abstract bool ReturnCodeActionAndRenamePathsWithPrefixedSlash { get; }

    /// <summary>
    /// Whether the file path for the generated C# documents should utilize the project key to
    /// ensure a unique file path per project.
    /// </summary>
    public abstract bool IncludeProjectKeyInGeneratedFilePath { get; }

    public abstract bool UseRazorCohostServer { get; }

    /// <summary>
    /// Indicates that client supports soft selection in completion list, meaning that typing a commit 
    /// character with a soft-selected item will not commit that item.
    /// </summary>
    public abstract bool SupportsSoftSelectionInCompletion { get; }

    /// <summary>
    /// Indicates that VSCode-compatible completion trigger character set should be used
    /// </summary>
    public abstract bool UseVsCodeCompletionCommitCharacters { get; }
}
