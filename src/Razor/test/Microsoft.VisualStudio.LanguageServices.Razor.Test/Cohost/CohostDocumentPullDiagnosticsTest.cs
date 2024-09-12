// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

[UseExportProvider]
public class CohostDocumentPullDiagnosticsTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public Task IfStatements()
        => VerifyDiagnosticsAsync("""
            <div></div>
            
            @code
            {
                public void IJustMetYou()
                {
                    {|CS0103:CallMeMaybe|}();
                }
            }
            """);

    private async Task VerifyDiagnosticsAsync(TestCode input)
    {
        var document = await CreateProjectAndRazorDocumentAsync(input.Text, createSeparateRemoteAndLocalWorkspaces: true);
        var inputText = await document.GetTextAsync(DisposalToken);

        var requestInvoker = new TestLSPRequestInvoker();

        var endpoint = new CohostDocumentPullDiagnosticsEndpoint(RemoteServiceInvoker, TestHtmlDocumentSynchronizer.Instance, requestInvoker, FilePathService, LoggerFactory);

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(document, DisposalToken);
        var actual = result!.SelectMany(d => d.Diagnostics!).ToArray();

        if (input.NamedSpans.Count == 0)
        {
            Assert.Null(result);
            return;
        }

        Assert.Equal(input.NamedSpans.Count, actual.Length);

        foreach (var (code, spans) in input.NamedSpans)
        {
            var diagnostic = Assert.Single(actual, d => d.Code == code);
            Assert.Equal(spans.First(), inputText.GetTextSpan(diagnostic.Range));
        }
    }
}
