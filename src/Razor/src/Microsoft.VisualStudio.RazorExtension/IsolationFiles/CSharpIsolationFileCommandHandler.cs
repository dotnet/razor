// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.RazorExtension.IsolationFiles;

/// <summary>
/// Command handler for C# code-behind isolation files (.razor.cs).
/// </summary>
internal sealed class CSharpIsolationFileCommandHandler(IServiceProvider serviceProvider)
    : IsolationFileCommandHandler(serviceProvider, ".cs")
{
    protected override string AddText => Resources.Add_Code_Behind_File;

    protected override string ViewText => Resources.View_Code_Behind_File;

    protected override string GenerateFileContent(string razorFilePath, string componentOrViewName)
    {
        // Get namespace from the project's RootNamespace + relative path,
        // matching how the Razor compiler resolves namespaces.
        var namespaceName = GetNamespaceForRazorFile(razorFilePath) ?? "MyApp";

        return $$"""
            namespace {{namespaceName}}
            {
                public partial class {{componentOrViewName}}
                {
                }
            }
            """;
    }

    /// <summary>
    /// Gets the namespace for a Razor file by reading the project's RootNamespace property
    /// and computing the relative path, matching the Razor compiler's namespace resolution.
    /// </summary>
    private string? GetNamespaceForRazorFile(string razorFilePath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            var projectItem = FindProjectItem(razorFilePath);
            if (projectItem?.ContainingProject is EnvDTE.Project project)
            {
                var rootNamespace = project.Properties.Item("RootNamespace")?.Value as string;
                if (!rootNamespace.IsNullOrEmpty())
                {
                    if (Path.GetDirectoryName(project.FullName) is not string projectDir
                        || Path.GetDirectoryName(razorFilePath) is not string fileDir
                        || !fileDir.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase)
                        || (fileDir.Length > projectDir.Length
                            && fileDir[projectDir.Length] != Path.DirectorySeparatorChar
                            && fileDir[projectDir.Length] != Path.AltDirectorySeparatorChar))
                    {
                        return rootNamespace;
                    }

                    var relativePath = fileDir[projectDir.Length..]
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (relativePath.IsNullOrEmpty())
                    {
                        return rootNamespace;
                    }

                    // Convert path separators to dots, matching how the Razor compiler computes namespaces
                    var relativeNamespace = relativePath
                        .Replace(Path.DirectorySeparatorChar, '.')
                        .Replace(Path.AltDirectorySeparatorChar, '.');

                    return rootNamespace + "." + relativeNamespace;
                }
            }
        }
        catch
        {
        }

        return null;
    }
}
