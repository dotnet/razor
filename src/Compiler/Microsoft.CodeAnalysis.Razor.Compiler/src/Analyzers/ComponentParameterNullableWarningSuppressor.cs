// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Resources;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Razor.Compiler.Analyzers;

#pragma warning disable RS1041 // Compiler extensions should be implemented in assemblies targeting netstandard2.0

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ComponentParameterNullableWarningSuppressor : DiagnosticSuppressor
{
    private static readonly LocalizableString Description = new LocalizableResourceString(nameof(AnalyzerResources.ComponentParameterNullableWarningSuppressorDescription), AnalyzerResources.ResourceManager, typeof(AnalyzerResources));

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [
            new SuppressionDescriptor("RZS1001", "CS8618", Description)
        ];

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var node = diagnostic.Location.SourceTree?.GetRoot().FindNode(diagnostic.Location.SourceSpan);
            if (node is PropertyDeclarationSyntax property && property.AttributeLists.Any(a => a.Attributes.Any(a => a.Name.ToString() == "EditorRequired")))
            {
                context.ReportSuppression(Suppression.Create(SupportedSuppressions[0], diagnostic));
            }
        }
    }
}

