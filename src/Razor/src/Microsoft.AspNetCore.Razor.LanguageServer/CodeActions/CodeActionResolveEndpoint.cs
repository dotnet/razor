// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.CodeActions;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

[RazorLanguageServerEndpoint(Methods.CodeActionResolveName)]
internal sealed class CodeActionResolveEndpoint(
    ICodeActionResolveService codeActionResolveService,
    RazorLSPOptionsMonitor razorLSPOptionsMonitor) : IRazorRequestHandler<CodeAction, CodeAction>
{
    private readonly ICodeActionResolveService _codeActionResolveService = codeActionResolveService;
    private readonly RazorLSPOptionsMonitor _razorLSPOptionsMonitor = razorLSPOptionsMonitor;

    public bool MutatesSolutionState => false;

    public TextDocumentIdentifier GetTextDocumentIdentifier(CodeAction request)
        => _codeActionResolveService.GetRazorCodeActionResolutionParams(request).TextDocument;

    public async Task<CodeAction> HandleRequestAsync(CodeAction request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var options = new RazorFormattingOptions
        {
            TabSize = _razorLSPOptionsMonitor.CurrentValue.TabSize,
            InsertSpaces = _razorLSPOptionsMonitor.CurrentValue.InsertSpaces,
            CodeBlockBraceOnNextLine = _razorLSPOptionsMonitor.CurrentValue.CodeBlockBraceOnNextLine
        };
        var documentContext = requestContext.DocumentContext.AssumeNotNull();

        return await _codeActionResolveService.ResolveCodeActionAsync(documentContext, request, options, cancellationToken).ConfigureAwait(false);

    }
}
