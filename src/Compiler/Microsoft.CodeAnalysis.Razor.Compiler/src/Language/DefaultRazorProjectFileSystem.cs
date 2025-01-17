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

        if (!absolutePath.StartsWith(absoluteBasePath, StringComparison.OrdinalIgnoreCase))
        {
            return ThrowHelper.ThrowInvalidOperationException<RazorProjectItem>($"The file '{absolutePath}' is not a descendent of the base path '{absoluteBasePath}'.");
        }

        var physicalPath = Path.GetFullPath(absolutePath);
        var relativePhysicalPath = physicalPath[(absoluteBasePath.Length + 1)..]; // Don't include leading separator

        var filePath = "/" + relativePhysicalPath.Replace(Path.DirectorySeparatorChar, '/');

        return new DefaultRazorProjectItem(DefaultBasePath, filePath, physicalPath, relativePhysicalPath, fileKind, cssScope: null);
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

        var normalizedPath = path.Replace('\\', '/');

        // Check if the given path is an absolute path. It is absolute if...
        //
        // 1. It is a network share path and starts with a '//' (e.g. //server/some/network/folder) or...
        // 2. It starts with Root
        if (normalizedPath is ['/', '/', ..] ||
            normalizedPath.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath;
        }

        // This is not an absolute path, so we combine it with Root.

        using var builder = new MemoryBuilder<char>(initialCapacity: Root.Length + normalizedPath.Length + 1);

        // First, add Root.
        var rootSpan = Root.AsSpan();
        builder.Append(rootSpan);

        var pathSpan = normalizedPath.AsSpan();

        // If the root doesn't end in a '/', add one.
        if (rootSpan is not [.., '/'])
        {
            builder.Append('/');
        }

        // If the path starts with a '/', slice it out.
        if (pathSpan is ['/', ..])
        {
            pathSpan = pathSpan[1..];
        }

        // Finally, add the path.
        builder.Append(pathSpan);

        return builder.AsMemory().Span.ToString();
    }
}
