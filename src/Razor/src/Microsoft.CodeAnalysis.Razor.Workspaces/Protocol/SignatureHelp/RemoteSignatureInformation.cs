// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Protocol.SignatureHelp;

[DataContract]
internal readonly record struct RemoteSignatureInformation(
    [property: DataMember(Order = 0)] string Label)
{
    internal SignatureInformation ToSignatureInformation()
    {
        return new SignatureInformation()
        {
            Label = this.Label,
            Documentation = new MarkupContent()
            {
            }
        };
    }
}
