// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Compiler.CSharp;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

internal sealed class Utf8WriteLiteralDetectionPass : IntermediateNodePassBase, IRazorOptimizationPass
{
    private const string ProbeTypeMetadataName = "__RazorUtf8WriteLiteralProbeNamespace.__RazorUtf8WriteLiteralProbeType";

    private IMetadataReferenceFeature? _referenceFeature;

    protected override void OnInitialized()
    {
        Engine.TryGetFeature(out _referenceFeature);
    }

    protected override void ExecuteCore(
        RazorCodeDocument codeDocument,
        DocumentIntermediateNode documentNode,
        CancellationToken cancellationToken)
    {
        if (!codeDocument.FileKind.IsLegacy() ||
            documentNode.Options is null ||
            documentNode.Options.DesignTime ||
            documentNode.Options.WriteHtmlUtf8StringLiterals)
        {
            return;
        }

        var references = _referenceFeature?.References;
        if (references is null || references.Count == 0)
        {
            return;
        }

        var @class = documentNode.FindPrimaryClass();
        var baseType = @class?.BaseType;
        if (baseType is null || string.IsNullOrWhiteSpace(baseType.BaseType.Content))
        {
            return;
        }

        var sourceText = BuildProbeSource(baseType, GetUsingDirectives(documentNode));
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, codeDocument.ParserOptions.CSharpParseOptions, cancellationToken: cancellationToken);
        var compilation = CSharpCompilation.Create(
            "__RazorUtf8WriteLiteralProbe",
            [syntaxTree],
            references);

        if (compilation.HasCallableUtf8WriteLiteralOverload(ProbeTypeMetadataName))
        {
            documentNode.Options = documentNode.Options.WithFlags(writeHtmlUtf8StringLiterals: true);
        }
    }

    private static string BuildProbeSource(BaseTypeWithModel baseType, IEnumerable<string> usingDirectives)
    {
        var builder = new StringBuilder();
        foreach (var usingDirective in usingDirectives)
        {
            builder.Append("using ").Append(usingDirective).AppendLine(";");
        }

        builder.AppendLine("namespace __RazorUtf8WriteLiteralProbeNamespace");
        builder.AppendLine("{");
        builder.Append("    internal class __RazorUtf8WriteLiteralProbeType : ").Append(BuildBaseType(baseType)).AppendLine();
        builder.AppendLine("    {");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string BuildBaseType(BaseTypeWithModel baseType)
    {
        var builder = new StringBuilder(baseType.BaseType.Content);
        if (baseType.GreaterThan is not null)
        {
            builder.Append(baseType.GreaterThan.Content);
        }

        if (baseType.ModelType is not null)
        {
            builder.Append(baseType.ModelType.Content);
        }

        if (baseType.LessThan is not null)
        {
            builder.Append(baseType.LessThan.Content);
        }

        return builder.ToString();
    }

    private static IEnumerable<string> GetUsingDirectives(DocumentIntermediateNode documentNode)
    {
        var @namespace = documentNode.FindPrimaryNamespace();
        if (@namespace is null)
        {
            yield break;
        }

        foreach (var child in @namespace.Children)
        {
            if (child is UsingDirectiveIntermediateNode usingDirective)
            {
                yield return usingDirective.Content;
            }
        }
    }
}
