// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

internal class DefaultRazorProjectFileSystem : RazorProjectFileSystem
{
    public DefaultRazorProjectFileSystem(string root)
    {
        ArgHelper.ThrowIfNullOrEmpty(root);

        // If "/" is passed in, we want that to be the value of root. We don't want root to end up
        // as an empty string.
        if (root == DefaultBasePath)
        {
            Root = DefaultBasePath;
        }
        else
        {
            root = root.Replace('\\', '/').TrimEnd('/');

            // Was the entire string just '\\' or '/'? If so, that's an invalid path.
            // Just throw instead of setting Root to an empty string.
            if (root.Length == 0)
            {
                ThrowHelper.ThrowArgumentException(nameof(root), $"Invalid path provided.");
            }

            Root = root;
        }
    }

    public string Root { get; }

    public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath)
    {
        var absoluteBasePath = NormalizeAndEnsureValidPath(basePath);

        var directory = new DirectoryInfo(absoluteBasePath);
        if (!directory.Exists)
        {
            return [];
        }

        return directory
            .EnumerateFiles("*.cshtml", SearchOption.AllDirectories)
            .Concat(directory.EnumerateFiles("*.razor", SearchOption.AllDirectories))
            .Select(file =>
            {
                var relativePhysicalPath = file.FullName.Substring(absoluteBasePath.Length + 1); // Include leading separator
                var filePath = "/" + relativePhysicalPath.Replace(Path.DirectorySeparatorChar, '/');

                return new DefaultRazorProjectItem(basePath, filePath, relativePhysicalPath, fileKind: null, file, cssScope: null);
            });
    }

    public override RazorProjectItem GetItem(string path, string? fileKind)
    {
        var absoluteBasePath = Root;
        var absolutePath = NormalizeAndEnsureValidPath(path);

        var file = new FileInfo(absolutePath);
        if (!absolutePath.StartsWith(absoluteBasePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The file '{absolutePath}' is not a descendent of the base path '{absoluteBasePath}'.");
        }

        var relativePhysicalPath = file.FullName.Substring(absoluteBasePath.Length + 1); // Include leading separator
        var filePath = "/" + relativePhysicalPath.Replace(Path.DirectorySeparatorChar, '/');

        return new DefaultRazorProjectItem(DefaultBasePath, filePath, relativePhysicalPath, fileKind, new FileInfo(absolutePath), cssScope: null);
    }

    protected override string NormalizeAndEnsureValidPath(string path)
    {
        // PERF: If we're asked to normalize "/", there's no need to compare and manipulate strings to
        // ultimately return the value of Root.
        if (path == DefaultBasePath)
        {
            return Root;
        }

        ArgHelper.ThrowIfNullOrEmpty(path);

        var absolutePath = path.Replace('\\', '/');

        // Check if the given path is an absolute path. It is absolute if,
        // 1. It starts with Root or
        // 2. It is a network share path and starts with a '//'. Eg. //servername/some/network/folder
        if (!absolutePath.StartsWith(Root, StringComparison.OrdinalIgnoreCase) &&
            !absolutePath.StartsWith("//", StringComparison.OrdinalIgnoreCase))
        {
            // This is not an absolute path. Strip the leading slash if any and combine it with Root.
            if (path[0] == '/' || path[0] == '\\')
            {
                path = path.Substring(1);
            }

            // Instead of `C:filename.ext`, we want `C:/filename.ext`.
            absolutePath = Root.EndsWith(':') && !path.IsNullOrEmpty()
                ? Root + "/" + path
                : Path.Combine(Root, path);
        }

        absolutePath = absolutePath.Replace('\\', '/');

        return absolutePath;
    }
}
