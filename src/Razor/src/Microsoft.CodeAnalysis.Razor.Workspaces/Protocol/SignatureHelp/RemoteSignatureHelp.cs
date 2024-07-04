// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Protocol.SignatureHelp;

[DataContract]
internal readonly record struct RemoteSignatureHelp(
    [property: DataMember(Order = 0)] int? ActiveParameter,
    [property: DataMember(Order = 1)] int? ActiveSignature,
    [property: DataMember(Order = 2)] RemoteSignatureInformation[] Signatures)
{
    public LSP.SignatureHelp ToSignatureHelp()
    {
        return new LSP.SignatureHelp()
        {
            ActiveParameter = this.ActiveParameter,
            ActiveSignature = this.ActiveSignature,
            Signatures = this.Signatures.Select(s => s.ToSignatureInformation()).ToArray()
        };
    }
}
