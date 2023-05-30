// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.AspNetCore.Razor.Language;

public static class CommonMetadata
{
    internal static readonly KeyValuePair<string, string?> BindAttributeGetSet
        = IsTrue(ComponentMetadata.Bind.BindAttributeGetSet);
    internal static readonly KeyValuePair<string, string?> IsDirectiveAttribute
        = IsTrue(ComponentMetadata.Common.DirectiveAttribute);
    internal static readonly KeyValuePair<string, string?> IsWeaklyTyped
        = IsTrue(ComponentMetadata.Component.WeaklyTypedKey);

    internal static KeyValuePair<string, string?> IsTrue(string key)
        => new(key, bool.TrueString);
    internal static KeyValuePair<string, string?> GloballyQualifiedTypeName(string value)
        => new(TagHelperMetadata.Common.GloballyQualifiedTypeName, value);
    public static KeyValuePair<string, string?> PropertyName(string value)
        => new(TagHelperMetadata.Common.PropertyName, value);

    internal static class Attributes
    {
        public static readonly MetadataCollection IsDirectiveAttribute = MetadataCollection.Create(CommonMetadata.IsDirectiveAttribute);
    }

    internal static class Parameters
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
