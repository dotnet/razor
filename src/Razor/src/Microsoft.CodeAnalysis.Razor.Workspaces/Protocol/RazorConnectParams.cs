// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Protocol;

[DataContract]
internal class RazorConnectParams
{
    [DataMember(Name = "pipeName")]
    public required string PipeName { get; set; }
}
