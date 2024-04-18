// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

internal partial class RazorDiagnosticsPublisher
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal sealed class TestAccessor(RazorDiagnosticsPublisher instance)
    {
        public bool IsWaitingToClearClosedDocuments => instance._documentClosedTimer is not null;
    }
}
