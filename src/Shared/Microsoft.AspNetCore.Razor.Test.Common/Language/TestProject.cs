// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.AspNetCore.Razor.Language;

public static class TestProject
{
    public static string GetProjectDirectory(string directoryHint, string layerFolderName, bool testDirectoryFirst = false)
    {
        var repoRoot = SearchUp(AppContext.BaseDirectory, "global.json");

        var projectDirectory = testDirectoryFirst
            ? Path.Combine(repoRoot, "src", layerFolderName, "test", directoryHint)
            : Path.Combine(repoRoot, "src", layerFolderName, directoryHint, "test");

        if (string.Equals(directoryHint, "Microsoft.AspNetCore.Razor.Language.Test", StringComparison.Ordinal))
        {
            Debug.Assert(!testDirectoryFirst);
            Debug.Assert(layerFolderName == "Compiler");
            projectDirectory = Path.Combine(repoRoot, "src", "Compiler", "Microsoft.AspNetCore.Razor.Language", "test");
        }

        if (!Directory.Exists(projectDirectory))
        {
            throw new InvalidOperationException(
                $@"Could not locate project directory for type {directoryHint}. Directory probe path: {projectDirectory}.");
        }

        return projectDirectory;
    }

    public static string GetProjectDirectory(Type type, string layerFolderName, bool useCurrentDirectory = false)
    {
        var baseDir = useCurrentDirectory ? Directory.GetCurrentDirectory() : AppContext.BaseDirectory;
        var repoRoot = SearchUp(baseDir, "global.json");
        var assemblyName = type.Assembly.GetName().Name;
        var projectDirectory = Path.Combine(repoRoot, "src", layerFolderName, assemblyName, "test");
        if (string.Equals(assemblyName, "Microsoft.AspNetCore.Razor.Language.Test", StringComparison.Ordinal))
        {
            Debug.Assert(layerFolderName == "Compiler");
            projectDirectory = Path.Combine(repoRoot, "src", "Compiler", "Microsoft.AspNetCore.Razor.Language", "test");
        }

        if (!Directory.Exists(projectDirectory))
        {
            throw new InvalidOperationException(
                $@"Could not locate project directory for type {type.FullName}. Directory probe path: {projectDirectory}.");
        }

        return projectDirectory;
    }

    private static string SearchUp(string baseDirectory, string fileName)
    {
        var directoryInfo = new DirectoryInfo(baseDirectory);
        do
        {
            var fileInfo = new FileInfo(Path.Combine(directoryInfo.FullName, fileName));
            if (fileInfo.Exists)
            {
                return fileInfo.DirectoryName;
            }
            directoryInfo = directoryInfo.Parent;
        }
        while (directoryInfo.Parent != null);

        throw new Exception($"File {fileName} could not be found in {baseDirectory} or its parent directories.");
    }
}
