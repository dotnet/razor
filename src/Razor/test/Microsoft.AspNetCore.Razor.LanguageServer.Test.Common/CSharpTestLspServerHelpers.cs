// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Common
{
    internal class CSharpTestLspServerHelpers
    {
        public static AdhocWorkspace CreateCSharpTestWorkspace(IEnumerable<(Uri documentUri, SourceText csharpSourceText)> files, ExportProvider exportProvider)
        {
            var workspace = TestWorkspace.Create() as AdhocWorkspace;

            // Add project and solution to workspace
            var projectInfo = ProjectInfo.Create(
                id: ProjectId.CreateNewId("TestProject"),
                version: VersionStamp.Default,
                name: "TestProject",
                assemblyName: "TestProject",
                language: LanguageNames.CSharp,
                filePath: "C:\\TestSolution\\TestProject.csproj");

            var solutionInfo = SolutionInfo.Create(
                id: SolutionId.CreateNewId("TestSolution"),
                version: VersionStamp.Default,
                projects: new ProjectInfo[] { projectInfo });

            workspace.AddSolution(solutionInfo);

            // Add document to workspace. We use an IVT method to create the DocumentInfo variable because there's a special constructor in Roslyn that will
            // help identify the document as belonging to Razor.
            var languageServerFactory = exportProvider.GetExportedValue<IRazorLanguageServerFactoryWrapper>();

            var documentCount = 0;
            foreach (var (documentUri, csharpSourceText) in files)
            {
                var documentFilePath = documentUri.AbsolutePath;
                var textAndVersion = TextAndVersion.Create(csharpSourceText, VersionStamp.Default, documentFilePath);
                var documentInfo = languageServerFactory.CreateDocumentInfo(
                    id: DocumentId.CreateNewId(projectInfo.Id),
                    name: "TestDocument" + documentCount,
                    filePath: documentFilePath,
                    loader: TextLoader.From(textAndVersion),
                    razorDocumentServiceProvider: TestRazorDocumentServiceProvider.Instance);

                workspace.AddDocument(documentInfo);
                documentCount++;
            }

            return workspace;
        }

        public static async Task<CSharpTestLspServer> CreateCSharpLspServerAsync(
            AdhocWorkspace workspace,
            ExportProvider exportProvider,
            ServerCapabilities serverCapabilities)
        {
            var clientCapabilities = new ClientCapabilities();
            var testLspServer = await CSharpTestLspServer.CreateAsync(workspace, exportProvider, clientCapabilities, serverCapabilities).ConfigureAwait(false);
            return testLspServer;
        }
    }
}
