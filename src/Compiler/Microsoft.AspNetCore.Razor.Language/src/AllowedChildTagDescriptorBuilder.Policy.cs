// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

public partial class AllowedChildTagDescriptorBuilder
{
    private sealed class Policy : TagHelperPooledObjectPolicy<AllowedChildTagDescriptorBuilder>
    {
        public static readonly Policy Instance = new();

        public override AllowedChildTagDescriptorBuilder Create() => new();

        public override bool Return(AllowedChildTagDescriptorBuilder builder)
        {
            builder._parent = null;

            builder.Name = null;
            builder.DisplayName = null;

            builder._diagnostics?.Clear();

            return true;
        }
    }
}
