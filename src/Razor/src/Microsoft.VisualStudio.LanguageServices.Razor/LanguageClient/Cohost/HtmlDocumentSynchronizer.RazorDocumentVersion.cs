// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

internal sealed partial class HtmlDocumentSynchronizer
{
    private readonly struct RazorDocumentVersion(int workspaceVersion, ChecksumWrapper checksum)
    {
        internal int WorkspaceVersion => workspaceVersion;
        internal ChecksumWrapper Checksum => checksum;

        public override string ToString()
            => $"Checksum {checksum} from workspace version {workspaceVersion}";
    }
}
