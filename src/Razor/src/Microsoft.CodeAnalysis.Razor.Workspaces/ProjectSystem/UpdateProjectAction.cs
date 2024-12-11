// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal interface IUpdateProjectAction
{
}

internal record RemoveDocumentAction(HostDocument OriginalDocument) : IUpdateProjectAction;

internal record AddDocumentAction(HostDocument NewDocument, TextLoader TextLoader) : IUpdateProjectAction;

internal record UpdateDocumentAction(HostDocument OriginalDocument, HostDocument NewDocument, TextLoader TextLoader) : IUpdateProjectAction;

internal record MoveDocumentAction(IProjectSnapshot OriginalProject, IProjectSnapshot DestinationProject, HostDocument Document, TextLoader TextLoader) : IUpdateProjectAction;

internal record HostProjectUpdatedAction(HostProject HostProject) : IUpdateProjectAction;

internal record ProjectWorkspaceStateChangedAction(ProjectWorkspaceState WorkspaceState) : IUpdateProjectAction;
