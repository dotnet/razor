// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Converters;
using System;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class DelegateCreationConverter<T> : CustomCreationConverter<T>
{
    private readonly Func<Type, T> _factory;

    public DelegateCreationConverter(Func<Type, T> factory)
    {
        _factory = factory;
    }

    public override T Create(Type objectType) => _factory(objectType);
}
