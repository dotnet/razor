// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [Export(typeof(LSPDocumentMappingProvider))]
    internal class DefaultLSPDocumentMappingProvider : LSPDocumentMappingProvider
    {
        private static readonly TextEdit[] EmptyEdits = Array.Empty<TextEdit>();

        private readonly LSPRequestInvoker _requestInvoker;

        [ImportingConstructor]
        public DefaultLSPDocumentMappingProvider(LSPRequestInvoker requestInvoker)
        {
            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            _requestInvoker = requestInvoker;
        }

        public async override Task<RazorMapToDocumentRangesResponse> MapToDocumentRangesAsync(RazorLanguageKind languageKind, Uri razorDocumentUri, Range[] projectedRanges, CancellationToken cancellationToken)
        {
            if (razorDocumentUri is null)
            {
                throw new ArgumentNullException(nameof(razorDocumentUri));
            }

            if (projectedRanges is null)
            {
                throw new ArgumentNullException(nameof(projectedRanges));
            }

            var mapToDocumentRangeParams = new RazorMapToDocumentRangesParams()
            {
                Kind = languageKind,
                RazorDocumentUri = razorDocumentUri,
                ProjectedRanges = projectedRanges
            };

            var documentMappingResponse = await _requestInvoker.CustomRequestServerAsync<RazorMapToDocumentRangesParams, RazorMapToDocumentRangesResponse>(
                LanguageServerConstants.RazorMapToDocumentRangesEndpoint,
                LanguageServerKind.Razor,
                mapToDocumentRangeParams,
                cancellationToken).ConfigureAwait(false);

            return documentMappingResponse;
        }

        public async override Task<Location[]> RemapLocationsAsync(Location[] locations, CancellationToken cancellationToken)
        {
            if (locations is null)
            {
                throw new ArgumentNullException(nameof(locations));
            }

            var remappedLocations = new List<Location>();
            foreach (var location in locations)
            {
                var uri = location.Uri;
                RazorLanguageKind languageKind;
                if (RazorLSPConventions.IsRazorCSharpFile(uri))
                {
                    languageKind = RazorLanguageKind.CSharp;
                }
                else if (RazorLSPConventions.IsRazorHtmlFile(uri))
                {
                    languageKind = RazorLanguageKind.Html;
                }
                else
                {
                    // This location doesn't point to a virtual razor file. No need to remap.
                    remappedLocations.Add(location);
                    continue;
                }

                var razorDocumentUri = RazorLSPConventions.GetRazorDocumentUri(uri);

                var mappingResult = await MapToDocumentRangesAsync(
                    languageKind,
                    razorDocumentUri,
                    new[] { location.Range },
                    cancellationToken).ConfigureAwait(false);

                if (mappingResult == null)
                {
                    // Couldn't remap the location. Discard.
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var remappedRange = mappingResult.Ranges[0];
                if (remappedRange.IsUndefined())
                {
                    // Couldn't remap the range correctly. Discard this range.
                    continue;
                }

                var remappedLocation = new Location()
                {
                    Uri = razorDocumentUri,
                    Range = remappedRange,
                };

                remappedLocations.Add(remappedLocation);
            }

            return remappedLocations.ToArray();
        }

        public async override Task<TextEdit[]> RemapTextEditsAsync(Uri uri, TextEdit[] edits, CancellationToken cancellationToken)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (edits is null)
            {
                throw new ArgumentNullException(nameof(edits));
            }

            var languageKind = RazorLanguageKind.Razor;
            if (RazorLSPConventions.IsRazorCSharpFile(uri))
            {
                languageKind = RazorLanguageKind.CSharp;
            }
            else if (RazorLSPConventions.IsRazorHtmlFile(uri))
            {
                languageKind = RazorLanguageKind.Html;
            }
            else
            {
                // This is not a virtual razor file. No need to remap.
                return edits;
            }

            var razorDocumentUri = RazorLSPConventions.GetRazorDocumentUri(uri);

            var rangesToMap = edits.Select(e => e.Range).ToArray();
            var mappingResult = await MapToDocumentRangesAsync(
                languageKind,
                razorDocumentUri,
                rangesToMap,
                cancellationToken).ConfigureAwait(false);

            if (mappingResult == null)
            {
                // Couldn't remap the location or the document changed in the meantime. Discard these ranges.
                return EmptyEdits;
            }

            var remappedEdits = new List<TextEdit>();
            for (var i = 0; i < edits.Length; i++)
            {
                var edit = edits[i];
                var range = mappingResult.Ranges[i];
                if (range.IsUndefined())
                {
                    // Couldn't remap the range correctly. Discard this range.
                    continue;
                }

                var remappedEdit = new TextEdit()
                {
                    Range = range,
                    NewText = edit.NewText
                };

                remappedEdits.Add(remappedEdit);
            }

            return remappedEdits.ToArray();
        }

        public async override Task<WorkspaceEdit> RemapWorkspaceEditAsync(WorkspaceEdit workspaceEdit, CancellationToken cancellationToken)
        {
            if (workspaceEdit?.DocumentChanges != null)
            {
                // The LSP spec says, we should prefer `DocumentChanges` property over `Changes` if available.
                var remappedEdits = await RemapVersionedDocumentEditsAsync(workspaceEdit.DocumentChanges, cancellationToken).ConfigureAwait(false);
                return new WorkspaceEdit()
                {
                    DocumentChanges = remappedEdits
                };
            }
            else if (workspaceEdit?.Changes != null)
            {
                var remappedEdits = await RemapDocumentEditsAsync(workspaceEdit.Changes, cancellationToken).ConfigureAwait(false);
                return new WorkspaceEdit()
                {
                    Changes = remappedEdits
                };
            }

            return workspaceEdit;
        }

        private async Task<TextDocumentEdit[]> RemapVersionedDocumentEditsAsync(TextDocumentEdit[] documentEdits, CancellationToken cancellationToken)
        {
            var remappedDocumentEdits = new List<TextDocumentEdit>();
            foreach (var entry in documentEdits)
            {
                var uri = entry.TextDocument.Uri;
                if (!CanRemap(uri))
                {
                    // This location doesn't point to a background razor file. No need to remap.
                    remappedDocumentEdits.Add(entry);

                    continue;
                }

                var edits = entry.Edits;
                var remappedEdits = await RemapTextEditsAsync(uri, edits, cancellationToken).ConfigureAwait(false);
                if (remappedEdits == null || remappedEdits.Length == 0)
                {
                    // Nothing to do.
                    continue;
                }

                var razorDocumentUri = RazorLSPConventions.GetRazorDocumentUri(uri);
                remappedDocumentEdits.Add(new TextDocumentEdit()
                {
                    TextDocument = new VersionedTextDocumentIdentifier()
                    {
                        Uri = razorDocumentUri,
                    },
                    Edits = remappedEdits
                });
            }

            return remappedDocumentEdits.ToArray();
        }

        private async Task<Dictionary<string, TextEdit[]>> RemapDocumentEditsAsync(Dictionary<string, TextEdit[]> changes, CancellationToken cancellationToken)
        {
            var remappedChanges = new Dictionary<string, TextEdit[]>();
            foreach (var entry in changes)
            {
                var uri = new Uri(entry.Key);
                var edits = entry.Value;

                if (!CanRemap(uri))
                {
                    // This location doesn't point to a background razor file. No need to remap.
                    remappedChanges[entry.Key] = entry.Value;
                    continue;
                }

                var remappedEdits = await RemapTextEditsAsync(uri, edits, cancellationToken).ConfigureAwait(false);
                if (remappedEdits == null || remappedEdits.Length == 0)
                {
                    // Nothing to do.
                    continue;
                }

                var razorDocumentUri = RazorLSPConventions.GetRazorDocumentUri(uri);
                remappedChanges[razorDocumentUri.AbsoluteUri] = remappedEdits;
            }

            return remappedChanges;
        }

        private static bool CanRemap(Uri uri)
        {
            return RazorLSPConventions.IsRazorCSharpFile(uri) || RazorLSPConventions.IsRazorHtmlFile(uri);
        }
    }
}
