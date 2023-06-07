﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Razor.Serialization.Converters;

namespace Microsoft.VisualStudio.LiveShare.Razor.Serialization;

internal static class LiveShareJsonConverterCollectionExtensions
{
    public static void RegisterRazorLiveShareConverters(this JsonConverterCollection collection)
    {
        if (collection is null)
        {
            throw new ArgumentNullException(nameof(collection));
        }

        if (collection.Contains(ProjectSnapshotHandleProxyJsonConverter.Instance))
        {
            // Already registered.
            return;
        }

        collection.Add(ProjectSnapshotHandleProxyJsonConverter.Instance);
        collection.RegisterRazorConverters();
    }
}
