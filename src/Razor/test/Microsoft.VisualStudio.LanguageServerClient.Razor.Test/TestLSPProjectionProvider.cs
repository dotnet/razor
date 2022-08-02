// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Test
{
    internal sealed class TestLSPProjectionProvider : LSPProjectionProvider
    {
        public static readonly TestLSPProjectionProvider Instance = new();

        private readonly DefaultRazorDocumentMappingService _mappingService = new(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), TestLoggerFactory.Instance);

        private TestLSPProjectionProvider()
        {
        }

        public override Task<ProjectionResult> GetProjectionAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
        {
            var text = documentSnapshot.Snapshot.GetText();
            var sourceText = SourceText.From(text);
            if (!position.TryGetAbsoluteIndex(sourceText, TestLogger.Instance, out var absoluteIndex))
            {
                return Task.FromResult<ProjectionResult>(null);
            }

            var codeDocument = HandlerTestBase.CreateCodeDocument(text, documentSnapshot.Uri.AbsolutePath);
            var languageKind = _mappingService.GetLanguageKind(codeDocument, absoluteIndex, rightAssociative: false);

            if (languageKind == RazorLanguageKind.CSharp)
            {
                if (!_mappingService.TryMapToProjectedDocumentPosition(codeDocument, absoluteIndex, out var projectedPosition, out var projectedIndex))
                {
                    return Task.FromResult<ProjectionResult>(null);
                }

                var vsProjectedPosition = new Position { Line = projectedPosition.Line, Character = projectedPosition.Character };
                if (documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpVirtualDocument))
                {
                    var projectionResult = new ProjectionResult
                    {
                        LanguageKind = RazorLanguageKind.CSharp,
                        Position = vsProjectedPosition,
                        PositionIndex = projectedIndex,
                        HostDocumentVersion = (int?)csharpVirtualDocument.HostDocumentSyncVersion,
                        Uri = csharpVirtualDocument.Uri
                    };

                    return Task.FromResult(projectionResult);
                }
            }

            // No C# projection, return original position
            var defaultProjection = new ProjectionResult
            {
                LanguageKind = languageKind,
                Position = position,
                PositionIndex = absoluteIndex,
                HostDocumentVersion = documentSnapshot.Version
            };

            return Task.FromResult(defaultProjection);
        }

        public override Task<ProjectionResult> GetProjectionForCompletionAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
            => GetProjectionAsync(documentSnapshot, position, cancellationToken);
    }
}
