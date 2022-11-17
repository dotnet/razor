﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

internal class TestRazorProjectFileSystem : DefaultRazorProjectFileSystem
{
    public new static RazorProjectFileSystem Empty = new TestRazorProjectFileSystem();

    private readonly Dictionary<string, RazorProjectItem> _lookup;

    public TestRazorProjectFileSystem()
        : this(Array.Empty<RazorProjectItem>())
    {
    }

    public TestRazorProjectFileSystem(IList<RazorProjectItem> items)
        : base("/")
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
