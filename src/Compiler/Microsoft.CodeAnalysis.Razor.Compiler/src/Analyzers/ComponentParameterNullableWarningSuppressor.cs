// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Razor.Compiler.Analyzers;

#pragma warning disable RS1041 // Compiler extensions should be implemented in assemblies targeting netstandard2.0

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComponentParameterNullableWarningSuppressor : DiagnosticSuppressor
{
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(AnalyzerResources.ComponentParameterNullableWarningSuppressorDescription), AnalyzerResources.ResourceManager, typeof(AnalyzerResources));

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [
            new SuppressionDescriptor(AnalyzerIDs.ComponentParameterNullableWarningSuppressionId, "CS8618", Description)
        ];

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var editorRequiredSymbol = context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.EditorRequiredAttribute");
        var parameterSymbol = context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.ParameterAttribute");

        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var node = diagnostic.Location.SourceTree?.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan);
            if (node is PropertyDeclarationSyntax propertySyntax && propertySyntax.AttributeLists.Any())
            {
                var symbol = context.GetSemanticModel(propertySyntax.SyntaxTree).GetDeclaredSymbol(propertySyntax, context.CancellationToken);
                if (symbol is IPropertySymbol property)
                {
                    if (IsEditorRequiredParam(property.GetAttributes()))
                    {
                        context.ReportSuppression(Suppression.Create(SupportedSuppressions[0], diagnostic));
                    }
                }
            }
        }

        bool IsEditorRequiredParam(ImmutableArray<AttributeData> attributes)
        {
            bool hasParameter = false, hasRequired = false;
            foreach (var attribute in attributes)
            {
                if (!hasParameter && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, parameterSymbol))
                {
                    hasParameter = true;
                    if (hasRequired)
                    {
                        break;
                    }
                    continue;
                }

                if (!hasRequired && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, editorRequiredSymbol))
                {
                    hasRequired = true;
                    if (hasParameter)
                    {
                        break;
                    }
                    continue;
                }
            }
            return hasParameter && hasRequired;
        }
    }
}

