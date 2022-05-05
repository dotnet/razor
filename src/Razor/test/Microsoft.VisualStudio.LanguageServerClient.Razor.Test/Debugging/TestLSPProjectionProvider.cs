// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Debugging
{
    internal class TestLSPProjectionProvider : LSPProjectionProvider
    {
        private readonly Uri _documentUri;
        private readonly IReadOnlyDictionary<Position, ProjectionResult> _customMappings;
        private readonly DefaultRazorDocumentMappingService _mappingService;

        public TestLSPProjectionProvider()
        {
            _mappingService = new DefaultRazorDocumentMappingService(TestLoggerFactory.Instance);
        }

        public TestLSPProjectionProvider(Uri documentUri, IReadOnlyDictionary<Position, ProjectionResult> customMappings) 
        {
            if (documentUri is null)
            {
                throw new ArgumentNullException(nameof(documentUri));
            }
            
            if (customMappings is null)
            {
                throw new ArgumentNullException(nameof(customMappings));
            }

            _documentUri = documentUri;
            _customMappings = customMappings;
            _mappingService = new DefaultRazorDocumentMappingService(TestLoggerFactory.Instance);
        }

        public override Task<ProjectionResult> GetProjectionAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
        {
            // Use custom mappings if provided, else fall back to the mapping service.
            if (_customMappings is null || _documentUri is null)
            {
                var text = documentSnapshot.Snapshot.GetText();
                var sourceText = SourceText.From(text);
                if (!position.TryGetAbsoluteIndex(sourceText, TestLogger.Instance, out var absoluteIndex))
                {
                    return Task.FromResult<ProjectionResult>(null);
                }

                var sourceDocument = TestRazorSourceDocument.Create(text, filePath: null, relativePath: null);
                var projectEngine = RazorProjectEngine.Create(builder => { });
                var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, FileKinds.Component, Array.Empty<RazorSourceDocument>(), Array.Empty<TagHelperDescriptor>());

                if (!_mappingService.TryMapToProjectedDocumentPosition(codeDocument, absoluteIndex, out var projectedPosition, out var projectedIndex))
                {
                    return Task.FromResult<ProjectionResult>(null);
                }

                var vsProjectedPosition = new Position { Line = projectedPosition.Line, Character = projectedPosition.Character };
                if (documentSnapshot.TryGetVirtualDocument <TestVirtualDocumentSnapshot>(out var testirtualDocument))
                {
                    var projectionResult = new ProjectionResult { Uri = testirtualDocument.Uri, Position = vsProjectedPosition, PositionIndex = projectedIndex };
                    return Task.FromResult(projectionResult);
                }

                return Task.FromResult<ProjectionResult>(null);
            }

            if (documentSnapshot.Uri != _documentUri)
            {
                return Task.FromResult<ProjectionResult>(null);
            }

            _customMappings.TryGetValue(position, out var customProjectionResult);

            return Task.FromResult(customProjectionResult);
        }

        public override Task<ProjectionResult> GetProjectionForCompletionAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
            => GetProjectionAsync(documentSnapshot, position, cancellationToken);
    }
}
