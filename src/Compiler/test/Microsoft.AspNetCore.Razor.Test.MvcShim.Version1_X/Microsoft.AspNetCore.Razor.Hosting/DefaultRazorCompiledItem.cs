﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Hosting;

internal class DefaultRazorCompiledItem : RazorCompiledItem
{
    private object[] _metadata;

    public DefaultRazorCompiledItem(Type type, string kind, string identifier)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (kind is null)
        {
            throw new ArgumentNullException(nameof(kind));
        }

        if (identifier is null)
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        Type = type;
        Kind = kind;
        Identifier = identifier;
    }

    public override string Identifier { get; }

    public override string Kind { get; }

    public override IReadOnlyList<object> Metadata
    {
        get
        {
            if (_metadata is null)
            {
                _metadata = Type.GetCustomAttributes(inherit: true);
            }

            return _metadata;
        }
    }

    public override Type Type { get; }
}
