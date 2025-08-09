// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.Language;

[NonCopyable]
internal ref struct MetadataBuilder
{
    private PooledSpanBuilder<KeyValuePair<string, string?>> _list;

    public MetadataBuilder()
    {
        _list = PooledSpanBuilder<KeyValuePair<string, string?>>.Empty;
    }

    public void Dispose()
    {
        ClearAndFree();
    }

    public void Add(string key, string? value)
        => _list.Add(new(key, value));

    public void Add(KeyValuePair<string, string?> pair)
        => Add(pair.Key, pair.Value);

    public MetadataCollection Build()
    {
        var result = MetadataCollection.CreateOrEmpty(_list.AsSpan());

        ClearAndFree();

        return result;
    }

    public void ClearAndFree()
    {
        _list.Dispose();
        _list = PooledSpanBuilder<KeyValuePair<string, string?>>.Empty;
    }
}
