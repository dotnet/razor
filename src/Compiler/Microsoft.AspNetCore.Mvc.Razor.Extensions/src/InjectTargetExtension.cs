﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class InjectTargetExtension : IInjectTargetExtension
{
    private const string RazorInjectAttribute = "[global::Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute]";

    public void WriteInjectProperty(CodeRenderingContext context, InjectIntermediateNode node)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var property = $"public {node.TypeName} {node.MemberName} {{ get; private set; }}";
        if (!context.Options.SuppressNullabilityEnforcement)
        {
            property += " = default!;";
        }

        if (node.Source.HasValue)
        {
            using (context.CodeWriter.BuildLinePragma(node.Source.Value, context))
            {
                WriteProperty();
            }
        }
        else
        {
            WriteProperty();
        }

        void WriteProperty()
        {
            if (!context.Options.SuppressNullabilityEnforcement)
            {
                context.CodeWriter.WriteLine("#nullable restore");
            }

            context.CodeWriter
                .WriteLine(RazorInjectAttribute)
                .WriteLine(property);

            if (!context.Options.SuppressNullabilityEnforcement)
            {
                context.CodeWriter.WriteLine("#nullable disable");
            }
        }
    }
}
