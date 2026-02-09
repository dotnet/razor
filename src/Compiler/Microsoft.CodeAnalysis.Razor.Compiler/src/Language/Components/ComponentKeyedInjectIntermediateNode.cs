// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal class ComponentKeyedInjectIntermediateNode : ExtensionIntermediateNode
{
    private ImmutableArray<string> injectedPropertyModifiers() { 
        return [
            $"[global::{ComponentsApi.InjectAttribute.FullTypeName}{(!string.IsNullOrEmpty(KeyName) ? "" : $"(Key = {KeyName})")}]",
            "private" // Encapsulation is the default
        ];
    }

    public ComponentKeyedInjectIntermediateNode(string typeName, string memberName, SourceSpan? typeSpan, SourceSpan? memberSpan, bool isMalformed, string keyName, SourceSpan? keySpan)
    {
        TypeName = typeName;
        MemberName = memberName;
        TypeSpan = typeSpan;
        MemberSpan = memberSpan;
        IsMalformed = isMalformed;
        KeyName = keyName;
        KeySource = keySpan;
    }

    public string TypeName { get; }

    public string MemberName { get; }

    public SourceSpan? TypeSpan { get; }

    public SourceSpan? MemberSpan { get; }

    public string KeyName { get; set; }

    public SourceSpan? KeySource { get; set; }

    public bool IsMalformed { get; }

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    public override void Accept(IntermediateNodeVisitor visitor)
    {
        if (visitor == null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        AcceptExtensionNode(this, visitor);
    }

    public override void WriteNode(CodeTarget target, CodeRenderingContext context)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (TypeName == string.Empty && TypeSpan.HasValue && !context.Options.DesignTime)
        {
            // if we don't even have a type name, just emit an empty mapped region so that intellisense still works
            using (context.BuildEnhancedLinePragma(TypeSpan.Value))
            {
            }
        }
        else
        {
            var memberName = MemberName ?? "Member_" + DefaultTagHelperTargetExtension.GetDeterministicId(context);

            if (!context.Options.DesignTime || !IsMalformed)
            {
                context.CodeWriter.WriteAutoPropertyDeclaration(
                    injectedPropertyModifiers(),
                    TypeName,
                    memberName,
                    TypeSpan,
                    MemberSpan,
                    context,
                    defaultValue: true);
            }
        }
    }
}
