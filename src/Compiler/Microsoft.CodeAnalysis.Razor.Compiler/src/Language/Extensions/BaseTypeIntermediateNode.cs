// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public sealed class BaseTypeIntermediateNode : ExtensionIntermediateNode
{
    const string ModelGenericParameter = "<TModel>";

    public override IntermediateNodeCollection Children { get; } = IntermediateNodeCollection.ReadOnly;

    // TODO: should we have a ctor that makes these directly for the static cases?
    public BaseTypeIntermediateNode(string baseType, SourceSpan? location)
    {
        // TODO: comment about this code
        if (baseType.EndsWith(ModelGenericParameter, System.StringComparison.Ordinal))
        {
            BaseType = IntermediateToken.CreateCSharpToken(baseType[0..^ModelGenericParameter.Length]);
            GreaterThan = IntermediateToken.CreateCSharpToken("<");
            ModelType = IntermediateToken.CreateCSharpToken("TModel");
            LessThan = IntermediateToken.CreateCSharpToken(">");

            if (location.HasValue)
            {
                var openBracketPosition = baseType.Length - ModelGenericParameter.Length;
                BaseType.Source = location.Value.Slice(0, openBracketPosition);
                GreaterThan.Source = location.Value.Slice(openBracketPosition, openBracketPosition + 1);
                ModelType.Source = location.Value.Slice(openBracketPosition + 1, baseType.Length - 1);
                LessThan.Source = location.Value.Slice(baseType.Length - 1, baseType.Length);
            }
        }
        else
        {
            BaseType = IntermediateToken.CreateCSharpToken(baseType, location);  
        }
        Source = location;
    }

    public IntermediateToken BaseType { get; set; }

    public IntermediateToken? GreaterThan { get; set; }

    public IntermediateToken? ModelType { get; set; }

    public IntermediateToken? LessThan { get; set; }

    public override void Accept(IntermediateNodeVisitor visitor)
    {
        AcceptExtensionNode(this, visitor);
    }

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        formatter.WriteProperty("Content", BaseType.Content + GreaterThan?.Content + ModelType?.Content + LessThan?.Content);
    }

    public override void WriteNode(CodeTarget target, CodeRenderingContext context)
    {
        WriteToken(BaseType);
        WriteToken(GreaterThan);
        WriteToken(ModelType);
        WriteToken(LessThan);

        void WriteToken(IntermediateToken? token)
        {
            if (token?.Source is not null)
            {
                context.CodeWriter.WriteWithPragma(token.Content, context, token.Source.Value);
            }
            else if (token is not null)
            {
                context.CodeWriter.Write(token.Content);
            }
        }

    }
}
