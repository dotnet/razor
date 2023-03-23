﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.AspNetCore.Razor.Language;

public static class TestProject
{
    public static string GetProjectDirectory(string directoryHint, bool testDirectoryFirst = false)
    {
        var repoRoot = SearchUp(AppContext.BaseDirectory, "global.json");
        if (repoRoot == null)
        {
            repoRoot = AppContext.BaseDirectory;
        }

        var projectDirectory = testDirectoryFirst
            ? Path.Combine(repoRoot, "src", "Compiler", "test", directoryHint)
            : Path.Combine(repoRoot, "src", "Compiler", directoryHint, "test");

        if (string.Equals(directoryHint, "Microsoft.AspNetCore.Razor.Language.Test", StringComparison.Ordinal))
        {
            Debug.Assert(!testDirectoryFirst);
            projectDirectory = Path.Combine(repoRoot, "src", "Compiler", "Microsoft.AspNetCore.Razor.Language", "test");
        }

        return projectDirectory;
    }

    public static string GetProjectDirectory(Type type)
    {
        var repoRoot = SearchUp(AppContext.BaseDirectory, "global.json");
        if (repoRoot == null)
        {
            repoRoot = AppContext.BaseDirectory;
        }

        var assemblyName = type.Assembly.GetName().Name;
        var projectDirectory = Path.Combine(repoRoot, "src", "Compiler", assemblyName, "test");
        if (string.Equals(assemblyName, "Microsoft.AspNetCore.Razor.Language.Test", StringComparison.Ordinal))
        {
            projectDirectory = Path.Combine(repoRoot, "src", "Compiler", "Microsoft.AspNetCore.Razor.Language", "test");
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

        return null;
    }
}
