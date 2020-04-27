// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Shared]
    [Export(typeof(RazorLanguageClientMiddleLayer))]
    internal class DefaultRazorLanguageClientMiddleLayer : RazorLanguageClientMiddleLayer
    {
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPEditorService _editorService;

        [ImportingConstructor]
        public DefaultRazorLanguageClientMiddleLayer(
            JoinableTaskContext joinableTaskContext,
            LSPDocumentManager documentManager,
            LSPEditorService editorService)
        {
            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (editorService is null)
            {
                throw new ArgumentNullException(nameof(editorService));
            }

            _joinableTaskFactory = joinableTaskContext.Factory;
            _documentManager = documentManager;
            _editorService = editorService;
        }

        public override bool CanHandle(string methodName)
        {
            return methodName == Methods.TextDocumentOnTypeFormattingName ||
                methodName == Methods.TextDocumentCompletionName;
        }

        public override Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
        {
            return null;
        }

        public override async Task<JToken> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken>> sendRequest)
        {
            if (methodName == Methods.TextDocumentOnTypeFormattingName)
            {
                var emptyResult = JToken.FromObject(Array.Empty<TextEdit>());
                var requestParams = methodParam.ToObject<DocumentOnTypeFormattingParams>();
                if (requestParams.Options.OtherOptions == null)
                {
                    requestParams.Options.OtherOptions = new Dictionary<string, object>();
                }

                requestParams.Options.OtherOptions[LanguageServerConstants.ExpectsCursorPlaceholderKey] = true;
                var token = JToken.FromObject(requestParams);
                var result = await sendRequest(token).ConfigureAwait(false);
                var edits = result?.ToObject<TextEdit[]>();
                if (edits == null)
                {
                    return emptyResult;
                }

                await _joinableTaskFactory.SwitchToMainThreadAsync();

                if (!_documentManager.TryGetDocument(requestParams.TextDocument.Uri, out var documentSnapshot))
                {
                    return emptyResult;
                }

                await _editorService.ApplyTextEditsAsync(requestParams.TextDocument.Uri, documentSnapshot.Snapshot, edits).ConfigureAwait(false);

                // We would have already applied the edits and moved the cursor. Return empty.
                return emptyResult;
            }
            else if (methodName == Methods.TextDocumentCompletionName)
            {
                var response = await sendRequest(methodParam).ConfigureAwait(false);
                var result = response?.ToObject<SumType<CompletionItem[], CompletionList>?>();
                if (!result.HasValue)
                {
                    return response;
                }

                result = SetCompletionIcon(result.Value);

                return JToken.FromObject(result);
            }
            else
            {
                return await sendRequest(methodParam).ConfigureAwait(false);
            }
        }

        // Internal for testing
        internal SumType<CompletionItem[], CompletionList>? SetCompletionIcon(SumType<CompletionItem[], CompletionList> completionResult)
        {
            var result = completionResult.Match<SumType<CompletionItem[], CompletionList>?>(
                items =>
                {
                    var newItems = items.Select(item => SetIcon(item)).ToArray();
                    return newItems;
                },
                list =>
                {
                    var newItems = list.Items.Select(item => SetIcon(item)).ToArray();
                    return new CompletionList()
                    {
                        Items = newItems,
                        IsIncomplete = list.IsIncomplete,
                    };
                },
                () => null);

            return result;

            static CompletionItem SetIcon(CompletionItem item)
            {
                ImageElement icon = null;
                if (item.IsTagHelperElementCompletion() || item.IsTagHelperAttributeCompletion())
                {
                    icon = new ImageElement(new ImageId(KnownMonikers.XMLAttribute.Guid, KnownMonikers.XMLAttribute.Id));
                }
                else if (item.TryGetRazorCompletionKind(out var kind) &&
                    (kind == RazorCompletionItemKind.DirectiveAttribute ||
                    kind == RazorCompletionItemKind.DirectiveAttributeParameter ||
                    kind == RazorCompletionItemKind.MarkupTransition))
                {
                    icon = new ImageElement(new ImageId(KnownMonikers.XMLAttribute.Guid, KnownMonikers.XMLAttribute.Id));
                }

                if (icon == null)
                {
                    return item;
                }

                return new VSCompletionItem()
                {
                    Label = item.Label,
                    Kind = item.Kind,
                    Detail = item.Detail,
                    Documentation = item.Documentation,
                    Preselect = item.Preselect,
                    SortText = item.SortText,
                    FilterText = item.FilterText,
                    InsertText = item.InsertText,
                    InsertTextFormat = item.InsertTextFormat,
                    TextEdit = item.TextEdit,
                    AdditionalTextEdits = item.AdditionalTextEdits,
                    CommitCharacters = item.CommitCharacters,
                    Command = item.Command,
                    Data = item.Data,
                    Icon = icon,
                };
            }
        }
    }
}
