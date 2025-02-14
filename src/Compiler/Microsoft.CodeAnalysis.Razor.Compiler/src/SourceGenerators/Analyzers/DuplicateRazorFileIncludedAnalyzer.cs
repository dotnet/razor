
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.NET.Sdk.Razor.SourceGenerators;

namespace Microsoft.AspNetCore.Razor.SourceGenerators;

#pragma warning disable RS1041 // Compiler extensions should be implemented in assemblies targeting netstandard2.0
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1041 // Compiler extensions should be implemented in assemblies targeting netstandard2.0
public class DuplicateRazorFileIncludedAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = ImmutableArray.Create(RazorDiagnostics.DuplicateRazorFileIncludedDescriptor);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compStartAction =>
        {
            var includedFiles = new HashSet<string>(StringComparer.Ordinal);

            compStartAction.RegisterAdditionalFileAction(additionalFilesContext =>
            {
                var additionalFile = additionalFilesContext.AdditionalFile;
                var fileName = Path.GetFileName(additionalFile.Path);

                if (additionalFile.Path.EndsWith(".cshtml", StringComparison.Ordinal) ||
                    additionalFile.Path.EndsWith(".razor", StringComparison.Ordinal))
                {
                    if (!includedFiles.Add(additionalFile.Path))
                    {
                        var diagnostic = Diagnostic.Create(
                            RazorDiagnostics.DuplicateRazorFileIncludedDescriptor,
                            Location.Create(additionalFile.Path, new TextSpan(0, 0), new LinePositionSpan(LinePosition.Zero, LinePosition.Zero)),
                            additionalFile.Path);

                        additionalFilesContext.ReportDiagnostic(diagnostic);
                    }
                }
            });
        });
    }
}
