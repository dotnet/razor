// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

internal partial class RazorDiagnosticsPublisher
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal sealed class TestAccessor(RazorDiagnosticsPublisher instance)
    {
        public bool IsWaitingToClearClosedDocuments => instance._documentClosedTimer is not null;

        public void SetPublishedCSharpDiagnostics(string filePath, ImmutableArray<Diagnostic> diagnostics)
        {
            lock (instance._gate)
            {
                instance._publishedCSharpDiagnostics[filePath] = diagnostics;
            }
        }

        public void SetPublishedRazorDiagnostics(string filePath, ImmutableArray<RazorDiagnostic> diagnostics)
        {
            lock (instance._gate)
            {
                instance._publishedRazorDiagnostics[filePath] = diagnostics;
            }
        }
    }
}
