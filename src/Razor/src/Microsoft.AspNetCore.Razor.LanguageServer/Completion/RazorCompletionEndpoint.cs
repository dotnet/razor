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
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class RazorCompletionEndpoint : ICompletionHandler
    {
        private readonly ILogger _logger;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorCompletionFactsService _completionFactsService;
        private readonly CompletionListCache _completionListCache;
        private static readonly Command s_retriggerCompletionCommand = new()
        {
            Name = "editor.action.triggerSuggest",
            Title = RazorLS.Resources.ReTrigger_Completions_Title,
        };
        private PlatformAgnosticCompletionCapability? _capability;
        private IReadOnlyList<ExtendedCompletionItemKinds>? _supportedItemKinds;

        public RazorCompletionEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            RazorCompletionFactsService completionFactsService,
            CompletionListCache completionListCache,
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

            if (completionListCache is null)
            {
                throw new ArgumentNullException(nameof(completionListCache));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _completionFactsService = completionFactsService;
            _logger = loggerFactory.CreateLogger<RazorCompletionEndpoint>();
            _completionListCache = completionListCache;
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
            var completionOptions = new RazorCompletionOptions(SnippetsSupported: true);
            var completionContext = new RazorCompletionContext(syntaxTree, tagHelperDocumentContext, reason, completionOptions);

            var razorCompletionItems = _completionFactsService.GetCompletionItems(completionContext, location);

            _logger.LogTrace($"Resolved {razorCompletionItems.Count} completion items.");

            var completionList = CreateLSPCompletionList(razorCompletionItems);

            return completionList;
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
                    completionItem = completionItem.CreateWithCompletionListResultId(resultId, completionCapability);
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

            var razorCommitCharacters = razorCompletionItem.CommitCharacters?.Select(c => c.Character)?.ToArray() ?? Array.Empty<string>();
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

                        if (razorCommitCharacters.Length > 0)
                        {
                            directiveCompletionItem = directiveCompletionItem with { CommitCharacters = razorCommitCharacters };
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

                        if (razorCommitCharacters.Length > 0)
                        {
                            directiveAttributeCompletionItem = directiveAttributeCompletionItem with { CommitCharacters = razorCommitCharacters };
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

                        if (razorCommitCharacters.Length > 0)
                        {
                            markupTransitionCompletionItem = markupTransitionCompletionItem with { CommitCharacters = razorCommitCharacters };
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

                        if (razorCommitCharacters.Length > 0)
                        {
                            tagHelperElementCompletionItem = tagHelperElementCompletionItem with { CommitCharacters = razorCommitCharacters };
                        }

                        completionItem = tagHelperElementCompletionItem;
                        return true;
                    }
                case RazorCompletionItemKind.TagHelperAttribute:
                    {
                        var isSnippet = IsInsertTextSnippet(razorCompletionItem, out var snippetInsertText);
                        var tagHelperAttributeCompletionItem = new CompletionItem()
                        {
                            Label = razorCompletionItem.DisplayText,
                            InsertText = snippetInsertText,
                            FilterText = razorCompletionItem.InsertText,
                            SortText = razorCompletionItem.SortText,
                            InsertTextFormat = isSnippet ? InsertTextFormat.Snippet : InsertTextFormat.PlainText,
                            Kind = tagHelperCompletionItemKind,
                        };

                        if (razorCommitCharacters.Length > 0)
                        {
                            tagHelperAttributeCompletionItem = tagHelperAttributeCompletionItem with { CommitCharacters = razorCommitCharacters };
                        }

                        completionItem = tagHelperAttributeCompletionItem;
                        return true;
                    }
            }

            completionItem = null;
            return false;

            static bool IsInsertTextSnippet(RazorCompletionItem razorCompletionItem, out string insertText)
            {
                var attributeCompletionTypes = razorCompletionItem.GetAttributeCompletionTypes();
                // If the attribute is a boolean than just its name is a legal response. Therefor don't do the snippet
                if (attributeCompletionTypes.Any(type => string.Equals(type, typeof(bool).ToString(), StringComparison.Ordinal)))
                {
                    insertText = razorCompletionItem.InsertText;
                    return false;
                }
                else
                {
                    insertText = $"{razorCompletionItem.InsertText}=\"$0\"";
                    return true;
                }
            }
        }
    }
}
