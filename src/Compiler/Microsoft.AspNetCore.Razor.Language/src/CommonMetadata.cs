// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.AspNetCore.Razor.Language;

internal static class CommonMetadata
{
    public static readonly KeyValuePair<string, string?> BindAttributeGetSet
        = new(ComponentMetadata.Bind.BindAttributeGetSet, bool.TrueString);

    public static KeyValuePair<string, string?> PropertyName(string value)
        => new(TagHelperMetadata.Common.PropertyName, value);

    public static class Parameters
    {
        public static readonly MetadataCollection After = MetadataCollection.Create(PropertyName("After"));
        public static readonly MetadataCollection Culture = MetadataCollection.Create(PropertyName("Culture"));
        public static readonly MetadataCollection Event = MetadataCollection.Create(PropertyName("Event"));
        public static readonly MetadataCollection Format = MetadataCollection.Create(PropertyName("Format"));
        public static readonly MetadataCollection Get = MetadataCollection.Create(PropertyName("Get"), BindAttributeGetSet);
        public static readonly MetadataCollection PreventDefault = MetadataCollection.Create(PropertyName("PreventDefault"));
        public static readonly MetadataCollection Set = MetadataCollection.Create(PropertyName("Set"));
        public static readonly MetadataCollection StopPropagation = MetadataCollection.Create(PropertyName("StopPropagation"));
    }
}
