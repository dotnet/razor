// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public sealed class LanguageServerWorkspace : Workspace
    {
        public LanguageServerWorkspace(HostServices host) : base(host, workspaceKind: "Custom")
        {
        }

        public override bool CanApplyChange(ApplyChangesKind feature) => true;

        public Project AddProject(string name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), name, name, LanguageNames.CSharp);

            OnProjectAdded(projectInfo);

            UpdateReferencesAfterAdd();

            return CurrentSolution.GetProject(projectInfo.Id);
        }

        public Document AddDocument(ProjectId projectId, string name, SourceText text)
        {
            if (projectId is null)
            {
                throw new ArgumentNullException(nameof(projectId));
            }

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var id = DocumentId.CreateNewId(projectId);
            var loader = TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create()));

            return AddDocument(DocumentInfo.Create(id, name, loader: loader));
        }

        public Document AddDocument(DocumentInfo documentInfo)
        {
            if (documentInfo is null)
            {
                throw new ArgumentNullException(nameof(documentInfo));
            }

            OnDocumentAdded(documentInfo);

            return CurrentSolution.GetDocument(documentInfo.Id);
        }

        protected override void Dispose(bool finalize)
        {
            // By default a workspace will kill connections when it is disposed and finalize: false.
            // This is our "hacky" way at ensuring connection infrastructure is not disposed. Once
            // Razor's infrastructure is decoupled from the C# workspace we can remove this hack:
            // https://github.com/dotnet/aspnetcore/issues/34126
            base.Dispose(finalize: true);
        }
    }
}
