// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
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
        private PlatformAgnosticCompletionCapability _capability;
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

        private IReadOnlyList<ExtendedCompletionItemKinds> _supportedItemKinds;

        public RazorCompletionEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            RazorCompletionFactsService completionFactsService,
            LSPTagHelperTooltipFactory lspTagHelperTooltipFactory,
            VSLSPTagHelperTooltipFactory vsLspTagHelperTooltipFactory,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
        {
            if (projectSnapshotManagerDispatcher == null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (documentResolver == null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (completionFactsService == null)
            {
                throw new ArgumentNullException(nameof(completionFactsService));
            }

            if (lspTagHelperTooltipFactory == null)
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

            if (loggerFactory == null)
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

        public void SetCapability(CompletionCapability capability)
        {
            _capability = (PlatformAgnosticCompletionCapability)capability;
            _supportedItemKinds = _capability.CompletionItemKind.ValueSet.Cast<ExtendedCompletionItemKinds>().ToList();
        }

        public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var document = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, CancellationToken.None);

            if (document is null || cancellationToken.IsCancellationRequested)
            {
                return new CompletionList(isIncomplete: false);
            }

            if (!IsApplicableTriggerContext(request.Context))
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
            var hostDocumentIndex = request.Position.GetAbsoluteIndex(sourceText);
            var location = new SourceSpan(hostDocumentIndex, 0);
            var triggerForIsIncomplete = request.Context.TriggerKind == CompletionTriggerKind.TriggerForIncompleteCompletions;
            var completionContext = new RazorCompletionContext(syntaxTree, tagHelperDocumentContext, triggerForIsIncomplete);

            var razorCompletionItems = _completionFactsService.GetCompletionItems(completionContext, location);

            _logger.LogTrace($"Resolved {razorCompletionItems.Count} completion items.");

            var completionList = CreateLSPCompletionList(razorCompletionItems);

            return completionList;
        }

        public CompletionRegistrationOptions GetRegistrationOptions()
        {
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
            if (associatedRazorCompletion == null)
            {
                Debug.Fail("Could not find an associated razor completion item. This should never happen since we were able to look up the cached completion list.");
                return Task.FromResult(completionItem);
            }

            // If the client is VS, also fill in the Description property.
            var useDescriptionProperty = _languageServer.ClientSettings.Capabilities is PlatformAgnosticClientCapabilities clientCapabilities &&
                clientCapabilities.SupportsVisualStudioExtensions;

            MarkupContent tagHelperMarkupTooltip = null;
            VSClassifiedTextElement tagHelperClassifiedTextTooltip = null;

            switch (associatedRazorCompletion.Kind)
            {
                case RazorCompletionItemKind.Directive:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetDirectiveCompletionDescription();
                        completionItem.Documentation = descriptionInfo.Description;
                        break;
                    }
                case RazorCompletionItemKind.MarkupTransition:
                    {
                        var descriptionInfo = associatedRazorCompletion.GetMarkupTransitionCompletionDescription();
                        completionItem.Documentation = descriptionInfo.Description;
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
                completionItem.Documentation = documentation;
            }

            if (tagHelperClassifiedTextTooltip != null)
            {
                var vsCompletionItem = completionItem.ToVSCompletionItem();
                vsCompletionItem.Documentation = string.Empty;
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
            IReadOnlyList<ExtendedCompletionItemKinds> supportedItemKinds,
            PlatformAgnosticCompletionCapability completionCapability)
        {
            var resultId = completionListCache.Set(razorCompletionItems);
            var completionItems = new List<CompletionItem>();
            foreach (var razorCompletionItem in razorCompletionItems)
            {
                if (TryConvert(razorCompletionItem, supportedItemKinds, out var completionItem))
                {
                    // The completion items are cached and can be retrieved via this result id to enable the "resolve" completion functionality.
                    completionItem.SetCompletionListResultId(resultId);
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
            IReadOnlyList<ExtendedCompletionItemKinds> supportedItemKinds,
            out CompletionItem completionItem)
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
                            SortText = razorCompletionItem.DisplayText,
                            Kind = CompletionItemKind.Struct,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            directiveCompletionItem.CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters);
                        }

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
                        var directiveAttributeCompletionItem = new CompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = razorCompletionItem.InsertText,
                            FilterText = razorCompletionItem.InsertText,
                            SortText = razorCompletionItem.InsertText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            directiveAttributeCompletionItem.CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters);
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
                            SortText = razorCompletionItem.InsertText,
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
                            SortText = razorCompletionItem.DisplayText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            markupTransitionCompletionItem.CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters);
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
                            SortText = razorCompletionItem.InsertText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            tagHelperElementCompletionItem.CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters);
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
                            SortText = razorCompletionItem.InsertText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        if (razorCompletionItem.CommitCharacters != null && razorCompletionItem.CommitCharacters.Count > 0)
                        {
                            tagHelperAttributeCompletionItem.CommitCharacters = new Container<string>(razorCompletionItem.CommitCharacters);
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
