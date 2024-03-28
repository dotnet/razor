﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

internal class TestRazorProjectFileSystem : DefaultRazorProjectFileSystem
{
    public static new RazorProjectFileSystem Empty = new TestRazorProjectFileSystem();

    private readonly Dictionary<string, RazorProjectItem> _lookup;

    public TestRazorProjectFileSystem()
        : this(Array.Empty<RazorProjectItem>())
    {
    }

    public TestRazorProjectFileSystem(IList<RazorProjectItem> items) : base("/")
    {
        _lookup = items.ToDictionary(item => item.FilePath);
    }

    public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath)
    {
        throw new NotImplementedException();
    }


    public override RazorProjectItem GetItem(string path)
    {
        return GetItem(path, fileKind: null);
    }

    public override RazorProjectItem GetItem(string path, string fileKind)
    {
        if (!_lookup.TryGetValue(path, out var value))
        {
            value = new NotFoundProjectItem("", path, fileKind);
        }

        return value;
    }
}
