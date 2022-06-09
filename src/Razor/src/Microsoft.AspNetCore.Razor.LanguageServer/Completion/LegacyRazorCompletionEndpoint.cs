﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    // The intention of this class is to temporarily exist as a snapshot in time for our pre-existing completion experience.
    // It will eventually be removed in favor of the non-Legacy variant at which point we'll also remove the feature flag
    // for this legacy version.
    internal class LegacyRazorCompletionEndpoint : IVSCompletionEndpoint
    {
        private readonly ILogger _logger;
        private readonly DocumentContextFactory _documentContextFactory;
        private readonly RazorCompletionFactsService _completionFactsService;
        private readonly CompletionListCache _completionListCache;
        private static readonly Command s_retriggerCompletionCommand = new()
        {
            CommandIdentifier = "editor.action.triggerSuggest",
            Title = RazorLS.Resources.ReTrigger_Completions_Title,
        };
        private VSInternalClientCapabilities? _clientCapabilities;

        public LegacyRazorCompletionEndpoint(
            DocumentContextFactory documentContextFactory,
            RazorCompletionFactsService completionFactsService,
            CompletionListCache completionListCache,
            ILoggerFactory loggerFactory)
        {
            if (documentContextFactory is null)
            {
                throw new ArgumentNullException(nameof(documentContextFactory));
            }

            if (completionFactsService is null)
            {
                throw new ArgumentNullException(nameof(completionFactsService));
            }

            if (completionListCache is null)
            {
                throw new ArgumentNullException(nameof(completionListCache));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _documentContextFactory = documentContextFactory;
            _completionFactsService = completionFactsService;
            _logger = loggerFactory.CreateLogger<RazorCompletionEndpoint>();
            _completionListCache = completionListCache;
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string AssociatedServerCapability = "completionProvider";

            _clientCapabilities = clientCapabilities;

            var registrationOptions = new CompletionOptions()
            {
                ResolveProvider = true,
                TriggerCharacters = new[] { "@", "<", ":" },
                AllCommitCharacters = new[] { ":", ">", " ", "=" },
            };

            return new RegistrationExtensionResult(AssociatedServerCapability, registrationOptions);
        }

        public async Task<VSInternalCompletionList?> Handle(VSCompletionParamsBridge request, CancellationToken cancellationToken)
        {
            var documentContext = await _documentContextFactory.TryCreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (request.Context is null || !IsApplicableTriggerContext(request.Context))
            {
                return null;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var syntaxTree = codeDocument.GetSyntaxTree();
            var tagHelperDocumentContext = codeDocument.GetTagHelperContext();

            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken);
            if (!request.Position.TryGetAbsoluteIndex(sourceText, _logger, out var hostDocumentIndex))
            {
                return null;
            }

            var reason = request.Context.TriggerKind switch
            {
                CompletionTriggerKind.TriggerForIncompleteCompletions => CompletionReason.Invoked,
                CompletionTriggerKind.Invoked => CompletionReason.Invoked,
                CompletionTriggerKind.TriggerCharacter => CompletionReason.Typing,
                _ => CompletionReason.Typing,
            };
            var completionOptions = new RazorCompletionOptions(SnippetsSupported: true);
            var queryableChange = new SourceChange(hostDocumentIndex, length: 0, newText: string.Empty);
            var owner = syntaxTree.Root.LocateOwner(queryableChange);
            var completionContext = new RazorCompletionContext(hostDocumentIndex, owner, syntaxTree, tagHelperDocumentContext, reason, completionOptions);

            var razorCompletionItems = _completionFactsService.GetCompletionItems(completionContext);

            _logger.LogTrace($"Resolved {razorCompletionItems.Count} completion items.");

            var completionList = CreateLSPCompletionList(razorCompletionItems);
            var completionCapability = _clientCapabilities?.TextDocument?.Completion as VSInternalCompletionSetting;

            // The completion items are cached and can be retrieved via this result id to enable the "resolve" completion functionality.
            var resultId = _completionListCache.Set(completionList, razorCompletionItems);
            completionList.SetResultId(resultId, completionCapability);

            return completionList;
        }

        // Internal for testing
        internal static bool IsApplicableTriggerContext(CompletionContext context)
        {
            if (context is not VSInternalCompletionContext vsCompletionContext)
            {
                Debug.Fail("Completion context should always be converted into a VSCompletionContext (even in VSCode).");

                // We do not support providing completions on delete.
                return false;
            }

            if (vsCompletionContext.InvokeKind == VSInternalCompletionInvokeKind.Deletion)
            {
                // We do not support providing completions on delete.
                return false;
            }

            return true;
        }

        // Internal for testing
        internal VSInternalCompletionList CreateLSPCompletionList(IReadOnlyList<RazorCompletionItem> razorCompletionItems) => CreateLSPCompletionList(razorCompletionItems, _clientCapabilities!);

        // Internal for benchmarking and testing
        internal static VSInternalCompletionList CreateLSPCompletionList(
            IReadOnlyList<RazorCompletionItem> razorCompletionItems,
            VSInternalClientCapabilities clientCapabilities)
        {
            var completionItems = new List<CompletionItem>();
            foreach (var razorCompletionItem in razorCompletionItems)
            {
                if (TryConvert(razorCompletionItem, clientCapabilities, out var completionItem))
                {
                    completionItems.Add(completionItem);
                }
            }

            var completionList = new VSInternalCompletionList()
            {
                Items = completionItems.ToArray(),
                IsIncomplete = false,
            };

            var completionCapability = clientCapabilities?.TextDocument?.Completion as VSInternalCompletionSetting;
            var optimizedCompletionList = CompletionListOptimizer.Optimize(completionList, completionCapability);
            return optimizedCompletionList;
        }

        // Internal for testing
        internal static bool TryConvert(
            RazorCompletionItem razorCompletionItem,
            VSInternalClientCapabilities clientCapabilities,
            [NotNullWhen(true)] out VSInternalCompletionItem? completionItem)
        {
            if (razorCompletionItem is null)
            {
                throw new ArgumentNullException(nameof(razorCompletionItem));
            }

            var tagHelperCompletionItemKind = CompletionItemKind.TypeParameter;
            var supportedItemKinds = clientCapabilities.TextDocument?.Completion?.CompletionItemKind?.ValueSet ?? Array.Empty<CompletionItemKind>();
            if (supportedItemKinds?.Contains(CompletionItemKind.TagHelper) == true)
            {
                tagHelperCompletionItemKind = CompletionItemKind.TagHelper;
            }

            var insertTextFormat = razorCompletionItem.IsSnippet ? InsertTextFormat.Snippet : InsertTextFormat.Plaintext;

            switch (razorCompletionItem.Kind)
            {
                case RazorCompletionItemKind.Directive:
                    {
                        var directiveCompletionItem = new VSInternalCompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.DisplayText,
                            SortText = razorCompletionItem.SortText,
                            InsertTextFormat = insertTextFormat,
                            Kind = CompletionItemKind.Struct,
                        };

                        directiveCompletionItem.UseCommitCharactersFrom(razorCompletionItem, clientCapabilities);

                        if (razorCompletionItem == DirectiveAttributeTransitionCompletionItemProvider.TransitionCompletionItem)
                        {
                            directiveCompletionItem.Command = s_retriggerCompletionCommand;
                            directiveCompletionItem.Kind = tagHelperCompletionItemKind;
                        }

                        completionItem = directiveCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.DirectiveAttribute:
                    {
                        var directiveAttributeCompletionItem = new VSInternalCompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.InsertText,
                            SortText = razorCompletionItem.SortText,
                            InsertTextFormat = insertTextFormat,
                            Kind = tagHelperCompletionItemKind,
                        };

                        directiveAttributeCompletionItem.UseCommitCharactersFrom(razorCompletionItem, clientCapabilities);

                        completionItem = directiveAttributeCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.DirectiveAttributeParameter:
                    {
                        var parameterCompletionItem = new VSInternalCompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.InsertText,
                            SortText = razorCompletionItem.SortText,
                            InsertTextFormat = insertTextFormat,
                            Kind = tagHelperCompletionItemKind,
                        };

                        parameterCompletionItem.UseCommitCharactersFrom(razorCompletionItem, clientCapabilities);

                        completionItem = parameterCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.MarkupTransition:
                    {
                        var markupTransitionCompletionItem = new VSInternalCompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.DisplayText,
                            SortText = razorCompletionItem.SortText,
                            InsertTextFormat = insertTextFormat,
                            Kind = tagHelperCompletionItemKind,
                        };

                        markupTransitionCompletionItem.UseCommitCharactersFrom(razorCompletionItem, clientCapabilities);

                        completionItem = markupTransitionCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.TagHelperElement:
                    {
                        var tagHelperElementCompletionItem = new VSInternalCompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.DisplayText,
                            SortText = razorCompletionItem.SortText,
                            InsertTextFormat = insertTextFormat,
                            Kind = tagHelperCompletionItemKind,
                        };

                        tagHelperElementCompletionItem.UseCommitCharactersFrom(razorCompletionItem, clientCapabilities);

                        completionItem = tagHelperElementCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.TagHelperAttribute:
                    {
                        var tagHelperAttributeCompletionItem = new VSInternalCompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.DisplayText,
                            SortText = razorCompletionItem.SortText,
                            InsertTextFormat = insertTextFormat,
                            Kind = tagHelperCompletionItemKind,
                        };

                        tagHelperAttributeCompletionItem.UseCommitCharactersFrom(razorCompletionItem, clientCapabilities);

                        completionItem = tagHelperAttributeCompletionItem;
                        return true;
                    }
            }

            completionItem = null;
            return false;
        }
    }
}
