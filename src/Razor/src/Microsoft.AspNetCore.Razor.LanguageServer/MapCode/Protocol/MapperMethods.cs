// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.LanguageServer.Protocol;

/// <summary>
/// Class which contains the string values for CodeMapper-related LSP messages.
/// </summary>
internal static class MapperMethods
{
    /// <summary>
    /// Method name for 'workspace/mapCode'.
    /// </summary>
    public const string WorkspaceMapCodeName = "workspace/mapCode";
    /// <summary>
    /// Strongly typed message object for 'workspace/mapCode'
    /// </summary>
    public readonly static LspRequest<MapCodeParams, WorkspaceEdit?> WorkspaceMapCode = new(WorkspaceMapCodeName);
}
