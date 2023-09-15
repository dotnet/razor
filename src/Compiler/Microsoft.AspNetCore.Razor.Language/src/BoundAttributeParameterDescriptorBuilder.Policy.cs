// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

public partial class BoundAttributeParameterDescriptorBuilder
{
    private sealed class Policy : TagHelperPooledObjectPolicy<BoundAttributeParameterDescriptorBuilder>
    {
        public static readonly Policy Instance = new();

        public override BoundAttributeParameterDescriptorBuilder Create() => new();

        public override bool Return(BoundAttributeParameterDescriptorBuilder builder)
        {
            builder._parent = null;
            builder._kind = null;
            builder._documentationObject = default;

            builder.Name = null;
            builder.TypeName = null;
            builder.IsEnum = false;
            builder.DisplayName = null;

            ClearDiagnostics(builder._diagnostics);
            builder._metadata.Clear();

            return true;
        }
    }
}
