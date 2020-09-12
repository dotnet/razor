// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class CodeActionEndpoint : ICodeActionHandler
    {
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly IEnumerable<RazorCodeActionProvider> _providers;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
        private readonly IClientLanguageServer _languageServer;

        private CodeActionCapability _capability;

        internal bool _supportsCodeActionResolve = false;

        public CodeActionEndpoint(
            RazorDocumentMappingService documentMappingService,
            IEnumerable<RazorCodeActionProvider> providers,
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            IClientLanguageServer languageServer,
            LanguageServerFeatureOptions languageServerFeatureOptions)
        {
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _foregroundDispatcher = foregroundDispatcher ?? throw new ArgumentNullException(nameof(foregroundDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
            _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
        }

        public CodeActionRegistrationOptions GetRegistrationOptions()
        {
            return new CodeActionRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector,
                CodeActionKinds = new[] {
                    CodeActionKind.RefactorExtract,
                    CodeActionKind.QuickFix,
                    CodeActionKind.Refactor
                }
            };
        }

        public void SetCapability(CodeActionCapability capability)
        {
            _capability = capability;

            var extendableClientCapabilities = _languageServer.ClientSettings?.Capabilities as ExtendableClientCapabilities;
            _supportsCodeActionResolve = extendableClientCapabilities?.SupportsCodeActionResolve ?? false;
        }

        public async Task<CommandOrCodeActionContainer> Handle(CodeActionParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var razorCodeActionContext = await GenerateRazorCodeActionContextAsync(request, cancellationToken).ConfigureAwait(false);
            if (razorCodeActionContext is null)
            {
                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
               return null;
            }

            var razorCodeActions = await GetRazorCodeActionsAsync(razorCodeActionContext, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
               return null;
            }

            var csharpCodeActions = await GetCSharpCodeActionsAsync(razorCodeActionContext, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
               return null;
            }

            var codeActions = Enumerable.Concat(
                razorCodeActions ?? Array.Empty<RazorCodeAction>(),
                csharpCodeActions ?? Array.Empty<RazorCodeAction>());

            if (!codeActions.Any())
            {
                return null;
            }

            // We must cast the RazorCodeAction into a platform compliant code action
            // For VS (SupportsCodeActionResolve = true) this means just encapsulating the RazorCodeAction in the `CommandOrCodeAction` struct
            // For VS Code (SupportsCodeActionResolve = false) we must convert it into a CodeAction or Command before encapsulating in the `CommandOrCodeAction` struct.
            var commandsOrCodeActions = codeActions.Select(c =>
                _supportsCodeActionResolve ? new CommandOrCodeAction(c) : c.AsVSCodeCommandOrCodeAction());

            return new CommandOrCodeActionContainer(commandsOrCodeActions);
        }

        private async Task<RazorCodeActionContext> GenerateRazorCodeActionContextAsync(CodeActionParams request, CancellationToken cancellationToken)
        {
            var documentSnapshot = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);

            if (documentSnapshot is null)
            {
                return null;
            }

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);

            var context = new RazorCodeActionContext(
                request,
                documentSnapshot,
                codeDocument,
                sourceText,
                _languageServerFeatureOptions.SupportsFileManipulation);

            return context;
        }

        private async Task<IEnumerable<RazorCodeAction>> GetCSharpCodeActionsAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
        {
            var csharpCodeActions = await GetCSharpCodeActionsFromLanguageServerAsync(context, cancellationToken);
            var filteredCSharpCodeActions = await FilterCSharpCodeActionsAsync(context, csharpCodeActions, cancellationToken);

            return filteredCSharpCodeActions;
        }

        private async Task<IEnumerable<RazorCodeAction>> FilterCSharpCodeActionsAsync(RazorCodeActionContext context, IEnumerable<RazorCodeAction> csharpCodeActions, CancellationToken cancellationToken)
        {
            var fqnDiagnostic = context.Request.Context.Diagnostics.FirstOrDefault(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error &&
                diagnostic.Code.HasValue &&
                diagnostic.Code.Value.IsString &&
                (diagnostic.Code.Value.String.Equals("CS0246", StringComparison.OrdinalIgnoreCase) || diagnostic.Code.Value.String.Equals("CS0103", StringComparison.OrdinalIgnoreCase)));

            if (fqnDiagnostic is null)
            {
                return default;
            }

            var codeRange = fqnDiagnostic.Range.AsTextSpan(context.SourceText);
            var associatedValue = context.SourceText.GetSubTextString(codeRange);

            var results = new HashSet<RazorCodeAction>();

            foreach (var codeAction in csharpCodeActions)
            {
                if (!codeAction.Title.Any(c => char.IsWhiteSpace(c)) &&
                    codeAction.Title.EndsWith(associatedValue, StringComparison.OrdinalIgnoreCase))
                {
                    var fqnCodeAction = CreateFQNCodeAction(context, fqnDiagnostic, codeAction);
                    results.Add(fqnCodeAction);

                    var addUsingCodeAction = CreateAddUsingCodeAction(context, codeAction);
                    if (addUsingCodeAction != null)
                    {
                        results.Add(addUsingCodeAction);
                    }
                }
            }

            return results;
        }

        private static RazorCodeAction CreateFQNCodeAction(RazorCodeActionContext context, Diagnostic fqnDiagnostic, RazorCodeAction codeAction)
        {
            var fqnWorkspaceEdit = new WorkspaceEdit()
            {
                Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>()
                {
                    {
                        context.Request.TextDocument.Uri,
                        new List<TextEdit>()
                        {
                            new TextEdit()
                            {
                                NewText = codeAction.Title,
                                Range = fqnDiagnostic.Range
                            }
                        }
                    }
                }
            };

            return new RazorCodeAction()
            {
                Title = codeAction.Title,
                Edit = fqnWorkspaceEdit
            };
        }

        private RazorCodeAction CreateAddUsingCodeAction(RazorCodeActionContext context, RazorCodeAction codeAction)
        {
            if (!DefaultRazorTagHelperBinderPhase.ComponentDirectiveVisitor.TrySplitNamespaceAndType(
                    codeAction.Title,
                    out var @namespaceSpan,
                    out _))
            {
                return default;
            }

            var @namespace = codeAction.Title.Substring(@namespaceSpan.Start, @namespaceSpan.Length);
            var addUsingStatement = $"@using {@namespace}";
            var addUsingWorkspaceEdit = new WorkspaceEdit()
            {
                Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>()
                {
                    {
                        context.Request.TextDocument.Uri,
                        new List<TextEdit>()
                        {
                            new TextEdit()
                            {
                                NewText = $"{addUsingStatement}\n",
                                Range = new Range(new Position(0, 0), new Position(0, 0))
                            }
                        }
                    }
                }
            };

            return new RazorCodeAction()
            {
                Title = addUsingStatement,
                Edit = addUsingWorkspaceEdit
            };
        }

        private async Task<IEnumerable<RazorCodeAction>> GetCSharpCodeActionsFromLanguageServerAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
        {
            Range projectedRange = null;
            if (context.Request.Range != null &&
                !_documentMappingService.TryMapToProjectedDocumentRange(
                    context.CodeDocument,
                    context.Request.Range,
                    out projectedRange))
            {
                return Array.Empty<RazorCodeAction>();
            }

            context.Request.Range = projectedRange;

            if (cancellationToken.IsCancellationRequested)
            {
               return null;
            }

            if (_supportsCodeActionResolve)
            {
                // Only VS has the Code Action Resolve ClientCapability
                // We must get the code actions from HTMLCSharpLanguageServer

            }
            else
            {
                // We must get the code actions from VSCode / Typescript LS
                var response = _languageServer.SendRequest(LanguageServerConstants.RazorGetCodeActionsEndpoint, context.Request);
                return await response.Returning<RazorCodeAction[]>(cancellationToken);
            }

            return Array.Empty<RazorCodeAction>();
        }

        private async Task<IEnumerable<RazorCodeAction>> GetRazorCodeActionsAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
        {
            var linePosition = new LinePosition(
                context.Request.Range.Start.Line,
                context.Request.Range.Start.Character);
            var hostDocumentIndex = context.SourceText.Lines.GetPosition(linePosition);
            context.Location = new SourceLocation(
                hostDocumentIndex,
                context.Request.Range.Start.Line,
                context.Request.Range.Start.Character);

            var tasks = new List<Task<RazorCodeAction[]>>();

            if (cancellationToken.IsCancellationRequested)
            {
               return null;
            }

            foreach (var provider in _providers)
            {
                var result = provider.ProvideAsync(context, cancellationToken);
                if (result != null)
                {
                    tasks.Add(result);
                }
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            var razorCodeActions = new List<RazorCodeAction>();

            if (cancellationToken.IsCancellationRequested)
            {
               return null;
            }

            for (var i = 0; i < results.Length; i++)
            {
                var result = results.ElementAt(i);

                if (!(result is null))
                {
                    razorCodeActions.AddRange(result);
                }
            }

            return razorCodeActions;
        }
    }
}
