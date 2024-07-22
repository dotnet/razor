// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using RLSP = Roslyn.LanguageServer.Protocol;
using VsDocumentHighlight = Microsoft.VisualStudio.LanguageServer.Protocol.DocumentHighlight;

namespace Microsoft.CodeAnalysis.Razor.Protocol.DocumentHighlight;

[DataContract]
internal readonly record struct RemoteDocumentHighlight(
    [property: DataMember(Order = 0)] LinePositionSpan Position,
    [property: DataMember(Order = 1)] DocumentHighlightKind Kind)
{
    public static RemoteDocumentHighlight FromRLSPDocumentHighlight(RLSP.DocumentHighlight h)
        => new RemoteDocumentHighlight(h.Range.ToLinePositionSpan(), (DocumentHighlightKind)h.Kind);

    public static VsDocumentHighlight ToLspDocumentHighlight(RemoteDocumentHighlight r)
        => new VsDocumentHighlight
        {
            Range = r.Position.ToRange(),
            Kind = r.Kind
        };
}
