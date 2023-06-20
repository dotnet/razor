// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.ProjectSystem;

internal interface IUpdateProjectAction
{
}

internal record RemoveDocumentAction(HostDocument OriginalDocument) : IUpdateProjectAction
{
}

internal record AddDocumentAction(HostDocument NewDocument, TextLoader TextLoader) : IUpdateProjectAction
{
}

internal record UpdateDocumentAction(HostDocument OriginalDocument, HostDocument NewDocument, TextLoader TextLoader) : IUpdateProjectAction
{
}

internal record MoveDocumentAction(IProjectSnapshot OriginalProject, IProjectSnapshot DestinationProject, HostDocument Document, TextLoader TextLoader) : IUpdateProjectAction
{
}
