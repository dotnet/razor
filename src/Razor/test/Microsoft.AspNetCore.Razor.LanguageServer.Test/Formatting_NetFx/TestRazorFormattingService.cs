﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

internal static class TestRazorFormattingService
{
    public static async Task<IRazorFormattingService> CreateWithFullSupportAsync(
        ILoggerFactory loggerFactory,
        RazorCodeDocument? codeDocument = null,
        RazorLSPOptions? razorLSPOptions = null)
    {
        codeDocument ??= TestRazorCodeDocument.CreateEmpty();

        var filePathService = new LSPFilePathService(TestLanguageServerFeatureOptions.Instance);
        var mappingService = new LspDocumentMappingService(filePathService, new TestDocumentContextFactory(), loggerFactory);

        var projectManager = StrictMock.Of<IProjectSnapshotManager>();

        var client = new FormattingLanguageServerClient(loggerFactory);
        client.AddCodeDocument(codeDocument);

        var configurationSyncService = new Mock<IConfigurationSyncService>(MockBehavior.Strict);
        configurationSyncService
            .Setup(c => c.GetLatestOptionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(razorLSPOptions));

        var optionsMonitor = TestRazorLSPOptionsMonitor.Create(
            configurationSyncService.Object);

        if (razorLSPOptions is not null)
        {
            await optionsMonitor.UpdateAsync(CancellationToken.None);
        }

        var passes = new List<IFormattingPass>()
        {
            new HtmlFormattingPass(mappingService, client, loggerFactory),
            new CSharpFormattingPass(mappingService, loggerFactory),
            new LspCSharpOnTypeFormattingPass(mappingService, loggerFactory),
            new LspRazorFormattingPass(mappingService, optionsMonitor),
            new FormattingDiagnosticValidationPass(mappingService, loggerFactory),
            new FormattingContentValidationPass(mappingService, loggerFactory),
        };

        return new RazorFormattingService(
            passes,
            new LspFormattingCodeDocumentProvider(),
            TestAdhocWorkspaceFactory.Instance);
    }
}
