// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class CodeActionResolutionEndpoint : IRazorCodeActionResolveHandler
    {
        private static readonly string CodeActionsResolveProviderCapability = "codeActionsResolveProvider";

        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly IReadOnlyDictionary<string, RazorCodeActionResolver> _resolvers;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly ILogger _logger;
        private readonly IClientLanguageServer _languageServer;

        public CodeActionResolutionEndpoint(
            RazorDocumentMappingService documentMappingService,
            IEnumerable<RazorCodeActionResolver> resolvers,
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            ILoggerFactory loggerFactory,
            IClientLanguageServer languageServer)
        {
            if (documentMappingService is null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            if (resolvers is null)
            {
                throw new ArgumentNullException(nameof(resolvers));
            }

            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            _documentMappingService = documentMappingService;
            _foregroundDispatcher = foregroundDispatcher;
            _documentResolver = documentResolver;
            _logger = loggerFactory.CreateLogger<CodeActionResolutionEndpoint>();
            _languageServer = languageServer;

            var resolverMap = new Dictionary<string, RazorCodeActionResolver>();
            foreach (var resolver in resolvers)
            {
                if (resolverMap.ContainsKey(resolver.Action))
                {
                    Debug.Fail($"Duplicate resolver action for {resolver.Action}.");
                }
                resolverMap[resolver.Action] = resolver;
            }
            _resolvers = resolverMap;
        }

        // Register VS LSP code action resolution server capability
        public RegistrationExtensionResult GetRegistration() => new RegistrationExtensionResult(CodeActionsResolveProviderCapability, true);

        public async Task<RazorCodeAction> Handle(RazorCodeAction request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!(request.Data is JObject paramsObj))
            {
                Debug.Fail($"Invalid CodeAction Received {request.Title}.");
                return request;
            }

            var resolutionParams = paramsObj.ToObject<RazorCodeActionResolutionParams>();

            if (resolutionParams.Action == LanguageServerConstants.CodeActions.CSharp)
            {
                return await ResolveCSharpCodeActionsFromLanguageServerAsync(request, resolutionParams, cancellationToken);
            }
            else
            {
                request.Edit = await GetWorkspaceEditAsync(resolutionParams, cancellationToken).ConfigureAwait(false);
                return request;
            }
        }

        // Internal for testing
        internal async Task<WorkspaceEdit> GetWorkspaceEditAsync(RazorCodeActionResolutionParams resolutionParams, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Resolving workspace edit for action `{resolutionParams.Action}`.");

            if (!_resolvers.TryGetValue(resolutionParams.Action, out var resolver))
            {
                Debug.Fail($"No resolver registered for {resolutionParams.Action}.");
                return null;
            }

            return await resolver.ResolveAsync(resolutionParams.Data as JObject, cancellationToken).ConfigureAwait(false);
        }

        private async Task<RazorCodeAction> ResolveCSharpCodeActionsFromLanguageServerAsync(
            RazorCodeAction codeAction,
            RazorCodeActionResolutionParams resolutionParams,
            CancellationToken cancellationToken)
        {
            if (!(resolutionParams.Data is JObject csharpParamsObj))
            {
                Debug.Fail($"Invalid CSharp CodeAction Received.");
                return null;
            }

            var csharpParams = csharpParamsObj.ToObject<CSharpCodeActionParams>();
            codeAction.Data = csharpParams.Data;

            var response = _languageServer.SendRequest(LanguageServerConstants.RazorResolveCodeActionsEndpoint, codeAction);
            var resolvedCodeAction = await response.Returning<RazorCodeAction>(cancellationToken);

            if (resolvedCodeAction.Edit?.DocumentChanges is null)
            {
                return resolvedCodeAction;
            }

            var documentSnapshot = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(csharpParams.RazorFileUri.GetAbsoluteOrUNCPath(), out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);

            if (documentSnapshot is null)
            {
                return null;
            }

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);

            var oldText = SourceText.From(codeDocument.GetCSharpDocument().GeneratedCode);
            var newText = SourceText.From(resolvedCodeAction.Edit.DocumentChanges.First().TextDocumentEdit.Edits.First().NewText);

            var changes = SourceTextDiffer.GetMinimalTextChanges(oldText, newText);

            var edits = new List<TextEdit>();

            foreach (var change in changes)
            {
                var csharpTextEdit = change.AsTextEdit(oldText);

                if (_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, csharpTextEdit.Range, out var razorTextEditRange))
                {
                    csharpTextEdit.Range = razorTextEditRange;
                    edits.Add(csharpTextEdit);
                }
            }

            var codeDocumentIdentifier = new VersionedTextDocumentIdentifier() { Uri = csharpParams.RazorFileUri };
            resolvedCodeAction.Edit = new WorkspaceEdit()
            {
                DocumentChanges = new List<WorkspaceEditDocumentChange>() {
                    new WorkspaceEditDocumentChange(
                        new TextDocumentEdit()
                        {
                            TextDocument = codeDocumentIdentifier,
                            Edits = edits,
                        }
                    )
                }
            };


            return resolvedCodeAction;
        }
    }
}
