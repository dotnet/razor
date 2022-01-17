// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Debugging
{
    internal class TestLSPProjectionProvider : LSPProjectionProvider
    {
        private readonly Uri _documentUri;
        private readonly IReadOnlyDictionary<Position, ProjectionResult> _mappings;

        public TestLSPProjectionProvider(Uri documentUri, IReadOnlyDictionary<Position, ProjectionResult> mappings)
        {
            if (documentUri is null)
            {
                throw new ArgumentNullException(nameof(documentUri));
            }

            if (mappings is null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            _documentUri = documentUri;
            _mappings = mappings;
        }

        public override Task<ProjectionResult> GetProjectionAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
        {
            if (documentSnapshot.Uri != _documentUri)
            {
                return Task.FromResult((ProjectionResult)null);
            }

            _mappings.TryGetValue(position, out var projectionResult);

            return Task.FromResult(projectionResult);
        }

        public override Task<ProjectionResult> GetProjectionForCompletionAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
            => GetProjectionAsync(documentSnapshot, position, cancellationToken);

        public override Task<ProjectionResult> GetNextCSharpPositionAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
        {
            if (documentSnapshot.Uri != _documentUri)
            {
                return Task.FromResult((ProjectionResult)null);
            }

            foreach (var mapping in _mappings.OrderBy(d => d.Key))
            {
                if (mapping.Key.Line >= position.Line &&
                    mapping.Key.Character >= position.Character)
                {
                    return Task.FromResult(mapping.Value);
                }
            }

            return Task.FromResult((ProjectionResult)null);
        }
    }
}
