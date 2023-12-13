// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

internal record RazorPullDiagnosticResponse(VSInternalDiagnosticReport[] CSharpDiagnostics, VSInternalDiagnosticReport[] HtmlDiagnostics, VSInternalDiagnosticReport[]? CSharpAdditionalDiagnostics)
{
    public int Length
        => CSharpDiagnostics.Length
        + HtmlDiagnostics.Length
        + (CSharpAdditionalDiagnostics?.Length ?? 0);

    public async Task AppendDiagnosticsAsync(RazorTranslateDiagnosticsService translateDiagnosticsService, VersionedDocumentContext documentContext, List<VSInternalDiagnosticReport> allDiagnostics, CancellationToken cancellationToken)
    {
        foreach (var report in CSharpDiagnostics)
        {
            if (report.Diagnostics is not null)
            {
                var mappedDiagnostics = await translateDiagnosticsService.TranslateAsync(RazorLanguageKind.CSharp, report.Diagnostics, documentContext, cancellationToken).ConfigureAwait(false);
                report.Diagnostics = mappedDiagnostics;
            }

            allDiagnostics.Add(report);
        }

        foreach (var report in HtmlDiagnostics)
        {
            if (report.Diagnostics is not null)
            {
                var mappedDiagnostics = await translateDiagnosticsService.TranslateAsync(RazorLanguageKind.Html, report.Diagnostics, documentContext, cancellationToken).ConfigureAwait(false);
                report.Diagnostics = mappedDiagnostics;
            }

            allDiagnostics.Add(report);
        }

        if (CSharpAdditionalDiagnostics is { } csharpAdditionalDiagnostics)
        {
            // No extra work needed, these were issued as a request for the razor/cshtml file already
            allDiagnostics.AddRange(csharpAdditionalDiagnostics);
        }
    }
}
