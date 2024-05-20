// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Protocol.Folding;

[DataContract]
internal class RemoteFoldingRange
{
    [DataMember(Order = 0)]
    public int StartLine { get; set; }

    [DataMember(Order = 1)]
    public int? StartCharacter { get; set; }

    [DataMember(Order = 2)]
    public int EndLine { get; set; }

    [DataMember(Order = 3)]
    public int? EndCharacter { get; set; }

    [DataMember(Order = 4)]
    public string? Kind { get; set; }

    [DataMember(Order = 5)]
    public string? CollapsedText { get; set; }

    public override string ToString()
    {
        return $"({StartLine}, {StartCharacter})-({EndLine}, {EndCharacter}), {Kind}, {CollapsedText}";
    }

    public static RemoteFoldingRange FromFoldingRange(FoldingRange r)
        => new RemoteFoldingRange
        {
            StartLine = r.StartLine,
            StartCharacter = r.StartCharacter,
            EndLine = r.EndLine,
            EndCharacter = r.EndCharacter,
            Kind = r.Kind?.Value,
            CollapsedText = r.CollapsedText,
        };

    public static FoldingRange ToFoldingRange(RemoteFoldingRange r)
        => new FoldingRange
        {
            StartLine = r.StartLine,
            StartCharacter = r.StartCharacter,
            EndLine = r.EndLine,
            EndCharacter = r.EndCharacter,
            Kind = r.Kind is null ? null : new FoldingRangeKind(r.Kind),
            CollapsedText = r.CollapsedText
        };
}
