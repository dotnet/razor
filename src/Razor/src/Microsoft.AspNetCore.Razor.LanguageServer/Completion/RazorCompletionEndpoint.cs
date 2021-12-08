// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class RazorCompletionEndpoint : ICompletionHandler, ICompletionResolveHandler
    {
        private readonly ILogger _logger;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorCompletionFactsService _completionFactsService;
        private readonly LSPTagHelperTooltipFactory _lspTagHelperTooltipFactory;
        private readonly VSLSPTagHelperTooltipFactory _vsLspTagHelperTooltipFactory;
        private readonly ClientNotifierServiceBase _languageServer;
        private readonly CompletionListCache _completionListCache;
        private static readonly Command s_retriggerCompletionCommand = new()
        {
            Name = "editor.action.triggerSuggest",
            Title = RazorLS.Resources.ReTrigger_Completions_Title,
        };
        private PlatformAgnosticCompletionCapability? _capability;
        private IReadOnlyList<ExtendedCompletionItemKinds>? _supportedItemKinds;

        // Guid is magically generated and doesn't mean anything. O# magic.
        public Guid Id => new Guid("011c77cc-f90e-4f2e-b32c-dafc6587ccd6");

        public RazorCompletionEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            RazorCompletionFactsService completionFactsService,
            LSPTagHelperTooltipFactory lspTagHelperTooltipFactory,
            VSLSPTagHelperTooltipFactory vsLspTagHelperTooltipFactory,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (completionFactsService is null)
            {
                throw new ArgumentNullException(nameof(completionFactsService));
            }

            if (lspTagHelperTooltipFactory is null)
            {
                throw new ArgumentNullException(nameof(lspTagHelperTooltipFactory));
            }

            if (vsLspTagHelperTooltipFactory is null)
            {
                throw new ArgumentNullException(nameof(vsLspTagHelperTooltipFactory));
            }

            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _completionFactsService = completionFactsService;
            _lspTagHelperTooltipFactory = lspTagHelperTooltipFactory;
            _vsLspTagHelperTooltipFactory = vsLspTagHelperTooltipFactory;
            _languageServer = languageServer;
            _logger = loggerFactory.CreateLogger<RazorCompletionEndpoint>();
            _completionListCache = new CompletionListCache();
        }

        public void SetCapability(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
        }

        public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            _capability = (PlatformAgnosticCompletionCapability)capability;
            if (_capability.CompletionItemKind is null)
            {
                throw new ArgumentNullException(nameof(CompletionItemKind), $"{nameof(CompletionItemKind)} was not supplied. Value is mandatory.");
            }

            _supportedItemKinds = _capability.CompletionItemKind.ValueSet.Cast<ExtendedCompletionItemKinds>().ToList();
            return new CompletionRegistrationOptions()
            {
                DocumentSelector = RazorDefaults.Selector,
                ResolveProvider = true,
                TriggerCharacters = new Container<string>("@", "<", ":"),

                // NOTE: This property is *NOT* processed in O# versions < 0.16
                // https://github.com/OmniSharp/csharp-language-server-protocol/blame/bdec4c73240be52fbb25a81f6ad7d409f77b5215/src/Protocol/Server/Capabilities/CompletionOptions.cs#L35-L44
                AllCommitCharacters = new Container<string>(":", ">", " ", "="),
            };
        }

        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var document = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, CancellationToken.None).ConfigureAwait(false);

            if (document is null || cancellationToken.IsCancellationRequested)
            {
                return new CompletionList(isIncomplete: false);
            }

            if (request.Context is null || !IsApplicableTriggerContext(request.Context))
            {
                return new CompletionList(isIncomplete: false);
            }

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return new CompletionList(isIncomplete: false);
            }

            var syntaxTree = codeDocument.GetSyntaxTree();
            var tagHelperDocumentContext = codeDocument.GetTagHelperContext();

            var sourceText = await document.GetTextAsync();
            if (!request.Position.TryGetAbsoluteIndex(sourceText, _logger, out var hostDocumentIndex))
            {
                return new CompletionList(isIncomplete: false);
            }

            var location = new SourceSpan(hostDocumentIndex, 0);
            var reason = request.Context.TriggerKind switch
            {
                CompletionTriggerKind.TriggerForIncompleteCompletions => CompletionReason.Invoked,
                CompletionTriggerKind.Invoked => CompletionReason.Invoked,
                CompletionTriggerKind.TriggerCharacter => CompletionReason.Typing,
                _ => CompletionReason.Typing,
            };
            var completionContext = new RazorCompletionContext(syntaxTree, tagHelperDocumentContext, reason);

            var razorCompletionItems = _completionFactsService.GetCompletionItems(completionContext, location);

            _logger.LogTrace($"Resolved {razorCompletionItems.Count} completion items.");

            var completionList = CreateLSPCompletionList(razorCompletionItems);

            return completionList;
        }

        public Task<CompletionItem> Handle(CompletionItem completionItem, CancellationToken cancellationToken)
        {
            if (!completionItem.TryGetCompletionListResultId(out var resultId))
            {
                // Couldn't resolve.
                return Task.FromResult(completionItem);
            }

            if (!_completionListCache.TryGet(resultId, out var cachedCompletionItems))
            {
                return Task.FromResult(completionItem);
            }

            var labelQuery = completionItem.Label;
            var associatedRazorCompletion = cachedCompletionItems.FirstOrDefault(completion => string.Equals(labelQuery, completion.DisplayText, StringComparison.Ordinal));
            if (associatedRazorCompletion is null)
            {
                Debug.Fail("Could not find an associated razor completion item. This should never happen since we were able to look up the cached completion list.");
                return Task.FromResult(completionItem);
            }

            // If the client is VS, also fill in the Description property.
            var useDescriptionProperty = _languageServer.ClientSettings.Capabilities is PlatformAgnosticClientCapabilities clientCapabilities &&
                clientCapabilities.SupportsVisualStudioExtensions;

            MarkupContent? tagHelperMarkupTooltip = null;
            VSClassifiedTextElement? tagHelperClassifiedTextTooltip = null;

            switch (associatedRazorCompletion.Kind)
            {
                case RazorCompletionItemKind.Directive:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetDirectiveCompletionDescription();
                        completionItem = completionItem with { Documentation = descriptionInfo.Description };
                        break;
                    }
                case RazorCompletionItemKind.MarkupTransition:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetMarkupTransitionCompletionDescription();
                        completionItem = completionItem with { Documentation = descriptionInfo.Description };
                        break;
                    }
                case RazorCompletionItemKind.DirectiveAttribute:
                case RazorCompletionItemKind.DirectiveAttributeParameter:
                case RazorCompletionItemKind.TagHelperAttribute:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetAttributeCompletionDescription();
                        if (useDescriptionProperty)
                        {
                            _vsLspTagHelperTooltipFactory.TryCreateTooltip(descriptionInfo, out tagHelperClassifiedTextTooltip);
                        }
                        else
                        {
                            _lspTagHelperTooltipFactory.TryCreateTooltip(descriptionInfo, out tagHelperMarkupTooltip);
                        }

                        break;
                    }
                case RazorCompletionItemKind.TagHelperElement:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetTagHelperElementDescriptionInfo();
                        if (useDescriptionProperty)
                        {
                            _vsLspTagHelperTooltipFactory.TryCreateTooltip(descriptionInfo, out tagHelperClassifiedTextTooltip);
                        }
                        else
                        {
                            _lspTagHelperTooltipFactory.TryCreateTooltip(descriptionInfo, out tagHelperMarkupTooltip);
                        }

                        break;
                    }
            }

            if (tagHelperMarkupTooltip != null)
            {
                var documentation = new StringOrMarkupContent(tagHelperMarkupTooltip);
                completionItem = completionItem with { Documentation = documentation };
            }

            if (tagHelperClassifiedTextTooltip != null)
            {
                var vsCompletionItem = completionItem.ToVSCompletionItem();
                completionItem = completionItem with { Documentation = string.Empty };
                vsCompletionItem.Description = tagHelperClassifiedTextTooltip;
                return Task.FromResult<CompletionItem>(vsCompletionItem);
            }

            return Task.FromResult(completionItem);
        }

        // Internal for testing
        internal static bool IsApplicableTriggerContext(CompletionContext context)
        {
            if (context is not OmniSharpVSCompletionContext vsCompletionContext)
            {
                Debug.Fail("Completion context should always be converted into a VSCompletionContext (even in VSCode).");

                // We do not support providing completions on delete.
                return false;
            }

            if (vsCompletionContext.InvokeKind == OmniSharpVSCompletionInvokeKind.Deletion)
            {
                // We do not support providing completions on delete.
                return false;
            }

            return true;
        }

        // Internal for testing
        internal CompletionList CreateLSPCompletionList(IReadOnlyList<RazorCompletionItem> razorCompletionItems) => CreateLSPCompletionList(razorCompletionItems, _completionListCache, _supportedItemKinds, _capability);

        // Internal for benchmarking and testing
        internal static CompletionList CreateLSPCompletionList(
            IReadOnlyList<RazorCompletionItem> razorCompletionItems,
            CompletionListCache completionListCache,
            IReadOnlyList<ExtendedCompletionItemKinds>? supportedItemKinds,
            PlatformAgnosticCompletionCapability? completionCapability)
        {
            var resultId = completionListCache.Set(razorCompletionItems);
            var completionItems = new List<CompletionItem>();
            foreach (var razorCompletionItem in razorCompletionItems)
            {
                if (TryConvert(razorCompletionItem, supportedItemKinds, out var completionItem))
                {
                    // The completion items are cached and can be retrieved via this result id to enable the "resolve" completion functionality.
                    completionItem = completionItem.CreateWithCompletionListResultId(resultId);
                    completionItems.Add(completionItem);
                }
            }

            var completionList = new CompletionList(completionItems, isIncomplete: false);

            // We wrap the pre-existing completion list with an optimized completion list to better control serialization/deserialization
            CompletionList optimizedCompletionList;

            if (completionCapability?.VSCompletionList != null)
            {
                // We're operating in VS, lets make a VS specific optimized completion list

                var vsCompletionList = VSCompletionList.Convert(completionList, completionCapability.VSCompletionList);
                optimizedCompletionList = new OptimizedVSCompletionList(vsCompletionList);
            }
            else
            {
                optimizedCompletionList = new OptimizedCompletionList(completionList);
            }

            return optimizedCompletionList;
        }

        // Internal for testing
        internal static bool TryConvert(
            RazorCompletionItem razorCompletionItem,
            IReadOnlyList<ExtendedCompletionItemKinds>? supportedItemKinds,
            [NotNullWhen(true)] out CompletionItem? completionItem)
        {
            if (razorCompletionItem is null)
            {
                throw new ArgumentNullException(nameof(razorCompletionItem));
            }

            var tagHelperCompletionItemKind = CompletionItemKind.TypeParameter;
            if (supportedItemKinds?.Contains(ExtendedCompletionItemKinds.TagHelper) == true)
            {
                tagHelperCompletionItemKind = (CompletionItemKind)ExtendedCompletionItemKinds.TagHelper;
            }

            switch (razorCompletionItem.Kind)
            {
                case RazorCompletionItemKind.Directive:
                    {
                        var directiveCompletionItem = new CompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.DisplayText,
                            SortText = razorCompletionItem.SortText,
                            Kind = CompletionItemKind.Struct,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            directiveCompletionItem = directiveCompletionItem with { CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters) };
                        }

                        if (razorCompletionItem == DirectiveAttributeTransitionCompletionItemProvider.TransitionCompletionItem)
                        {
                            directiveCompletionItem = directiveCompletionItem with
                            {
                                Command = s_retriggerCompletionCommand,
                                Kind = tagHelperCompletionItemKind,
                            };
                        }

                        completionItem = directiveCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.DirectiveAttribute:
                    {
                        var directiveAttributeCompletionItem = new CompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.InsertText,
                            SortText = razorCompletionItem.SortText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            directiveAttributeCompletionItem = directiveAttributeCompletionItem with { CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters) };
                        }

                        completionItem = directiveAttributeCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.DirectiveAttributeParameter:
                    {
                        var parameterCompletionItem = new CompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.InsertText,
                            SortText = razorCompletionItem.SortText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        completionItem = parameterCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.MarkupTransition:
                    {
                        var markupTransitionCompletionItem = new CompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.DisplayText,
                            SortText = razorCompletionItem.SortText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            markupTransitionCompletionItem = markupTransitionCompletionItem with { CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters) };
                        }

                        completionItem = markupTransitionCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.TagHelperElement:
                    {
                        var tagHelperElementCompletionItem = new CompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.InsertText,
                            SortText = razorCompletionItem.SortText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            tagHelperElementCompletionItem = tagHelperElementCompletionItem with { CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters) };
                        }

                        completionItem = tagHelperElementCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.TagHelperAttribute:
                    {
                        var tagHelperAttributeCompletionItem = new CompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.InsertText,
                            SortText = razorCompletionItem.SortText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            tagHelperAttributeCompletionItem = tagHelperAttributeCompletionItem with { CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters) };
                        }

                        completionItem = tagHelperAttributeCompletionItem;
                        return true;
                    }
            }

            completionItem = null;
            return false;
        }
    }
}
