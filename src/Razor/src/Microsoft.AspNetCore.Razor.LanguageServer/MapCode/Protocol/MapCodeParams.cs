// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.LanguageServer.Protocol;

/// <summary>
/// LSP Params for textDocument/mapCode calls.
/// </summary>
[DataContract]
internal class MapCodeParams
{
    /// <summary>
    /// Set of code blocks, associated with documents and regions, to map.
    /// </summary>
    [DataMember(Name = "mappings")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public MapCodeMapping[] Mappings { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Changes that should be applied to the workspace by the mapper before performing
    /// the mapping operation.
    /// </summary>
    [DataMember(Name = "updates")]
    public WorkspaceEdit? Updates
    {
        get;
        set;
    }
}
