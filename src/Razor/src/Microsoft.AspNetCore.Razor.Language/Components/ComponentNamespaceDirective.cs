// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Components
{
    internal class ComponentNamespaceDirective
    {
        public static readonly DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
            "namespace",
            DirectiveKind.SingleLine,
            builder =>
            {
                builder.AddNamespaceToken(
                    ComponentResources.NamespaceDirective_NamespaceToken_Name,
                    ComponentResources.NamespaceDirective_NamespaceToken_Description);
                builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
                builder.Description = ComponentResources.NamespaceDirective_Description;
            });

        public static RazorProjectEngineBuilder Register(RazorProjectEngineBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddDirective(Directive, FileKinds.Component, FileKinds.ComponentImport);
            return builder;
        }
    }
}
