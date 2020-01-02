// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using RoslynFormattingOptions = Microsoft.CodeAnalysis.Formatting.FormattingOptions;
using OmniSharpFormattingOptions = OmniSharp.Extensions.LanguageServer.Protocol.Models.FormattingOptions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class DefaultRazorFormattingService : RazorFormattingService
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly FilePathNormalizer _filePathNormalizer;
        private readonly ProjectSnapshotManagerAccessor _projectSnapshotManagerAccessor;
        private readonly ILanguageServer _server;

        public DefaultRazorFormattingService(
            ForegroundDispatcher foregroundDispatcher,
            RazorDocumentMappingService documentMappingService,
            FilePathNormalizer filePathNormalizer,
            ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor,
            ILanguageServer server)
        {
            if (foregroundDispatcher is null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (documentMappingService is null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            if (filePathNormalizer is null)
            {
                throw new ArgumentNullException(nameof(filePathNormalizer));
            }

            if (projectSnapshotManagerAccessor is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerAccessor));
            }

            if (server is null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _documentMappingService = documentMappingService;
            _filePathNormalizer = filePathNormalizer;
            _projectSnapshotManagerAccessor = projectSnapshotManagerAccessor;
            _server = server;
        }

        public override Task<TextEdit[]> FormatAsync(Uri uri, RazorCodeDocument codeDocument, Range range, OmniSharpFormattingOptions options)
        {
            var syntaxTree = codeDocument.GetSyntaxTree();
            var formattingSpans = syntaxTree.GetFormattingSpans();
            var indentations = GetLineIndentationMap(codeDocument.Source, formattingSpans);

            var edits = new List<TextEdit>();
            for (var i = (int)range.Start.Line; i <= (int)range.End.Line; i++)
            {
                var context = indentations[i];
                if (context.IndentationLevel == -1)
                {
                    // Couldn't determine the desired indentation. Leave this line alone.
                    continue;
                }

                var desiredIndentation = context.IndentationLevel * options.TabSize;

                if (context.FirstSpan.Kind == FormattingSpanKind.Code &&
                    context.ExistingIndentation >= desiredIndentation)
                {
                    // This is C# and it is already indented at least the minimum amount we require.
                    // Since we don't understand the structure of C#, it is better to leave this line alone.
                    continue;
                }

                var effectiveIndentation = desiredIndentation - context.ExistingIndentation;
                if (effectiveIndentation > 0)
                {
                    var indentationChar = options.InsertSpaces ? ' ' : '\t';
                    var indentationString = new string(indentationChar, (int)effectiveIndentation);
                    var edit = new TextEdit()
                    {
                        Range = new Range(new Position(i, 0), new Position(i, 0)),
                        NewText = indentationString,
                    };

                    edits.Add(edit);
                }
                else if (effectiveIndentation < 0)
                {
                    var edit = new TextEdit()
                    {
                        Range = new Range(new Position(i, 0), new Position(i, -effectiveIndentation)),
                        NewText = string.Empty,
                    };

                    edits.Add(edit);
                }
            }

            return Task.FromResult(edits.ToArray());
        }

        internal static Dictionary<int, IndentationContext> GetLineIndentationMap(RazorSourceDocument source, IReadOnlyList<FormattingSpan> formattingSpans)
        {
            var result = new Dictionary<int, IndentationContext>();
            var total = 0;
            for (var i = 0; i < source.Lines.Count; i++)
            {
                // Get first non-whitespace character position
                var lineLength = source.Lines.GetLineLength(i);
                var nonWsChar = 0;
                for (var j = 0; j < lineLength; j++)
                {
                    var ch = source[total + j];
                    if (!char.IsWhiteSpace(ch) && !ParserHelpers.IsNewLine(ch))
                    {
                        nonWsChar = j;
                        break;
                    }
                }

                // position now contains the first non-whitespace character or 0. Get the corresponding FormattingSpan.
                if (TryGetFormattingSpanIndex(total + nonWsChar, formattingSpans, out var index))
                {
                    var span = formattingSpans[index];
                    result[i] = new IndentationContext
                    {
                        Line = i,
                        IndentationLevel = span.IndentationLevel,
                        ExistingIndentation = nonWsChar,
                        FirstSpan = span,
                    };
                }
                else
                {
                    // Couldn't find a corresponding FormattingSpan.
                    result[i] = new IndentationContext
                    {
                        Line = i,
                        IndentationLevel = -1,
                        ExistingIndentation = nonWsChar,
                    };
                }

                total += lineLength;
            }

            return result;
        }

        internal static bool TryGetFormattingSpanIndex(int absoluteIndex, IReadOnlyList<FormattingSpan> formattingspans, out int index)
        {
            index = -1;
            for (var i = 0; i < formattingspans.Count; i++)
            {
                var formattingspan = formattingspans[i];
                var span = formattingspan.Span;

                if (span.Start <= absoluteIndex)
                {
                    if (span.End >= absoluteIndex)
                    {
                        if (span.End == absoluteIndex && span.Length > 0)
                        {
                            // We're at an edge.
                            // Non-marker spans do not own the edges after it
                            continue;
                        }

                        index = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task<TextEdit[]> FormatProjectedHtmlDocument(
            RazorCodeDocument codeDocument,
            Range range,
            string documentPath,
            OmniSharpFormattingOptions options)
        {
            var @params = new RazorDocumentRangeFormattingParams()
            {
                Kind = RazorLanguageKind.CSharp,
                ProjectedRange = range,
                HostDocumentFilePath = _filePathNormalizer.Normalize(documentPath),
                Options = options
            };

            var result = await _server.Client.SendRequest<RazorDocumentRangeFormattingParams, RazorDocumentRangeFormattingResponse>(
                "razor/rangeFormatting", @params);

            return result.Edits;
        }

        private async Task<TextEdit[]> FormatProjectedCSharpDocument(RazorCodeDocument codeDocument, OmniSharpFormattingOptions options)
        {
            var workspace = _projectSnapshotManagerAccessor.Instance.Workspace;

            var cSharpOptions = workspace.Options
                .WithChangedOption(RoslynFormattingOptions.TabSize, LanguageNames.CSharp, (int)options.TabSize)
                .WithChangedOption(RoslynFormattingOptions.UseTabs, LanguageNames.CSharp, !options.InsertSpaces);
            
            var csharpSpans = new List<TextSpan>();
            var csharpDocument = codeDocument.GetCSharpDocument();
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpDocument.GeneratedCode);
            var sourceText = SourceText.From(csharpDocument.GeneratedCode);
            var root = await syntaxTree.GetRootAsync();
            foreach (var mapping in csharpDocument.SourceMappings)
            {
                var span = new TextSpan(mapping.GeneratedSpan.AbsoluteIndex, mapping.GeneratedSpan.Length);
                csharpSpans.Add(span);
            }

            // Actually format all the C# parts of the document.
            var textChanges = Formatter.GetFormattedTextChanges(root, csharpSpans, workspace, options: cSharpOptions);

            var csharpEdits = new List<TextEdit>();
            foreach (var change in textChanges)
            {
                csharpEdits.Add(change.AsTextEdit(sourceText));
            }

            // We now have the edits for the projected C# document. We need to map these back to the original razor document.
            var actualEdits = MapProjectedCSharpEdits(codeDocument, csharpEdits);
            return actualEdits;
        }

        private TextEdit[] MapProjectedCSharpEdits(RazorCodeDocument codeDocument, List<TextEdit> csharpEdits)
        {
            var actualEdits = new List<TextEdit>();
            foreach (var edit in csharpEdits)
            {
                if (_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, edit.Range, out var newRange))
                {
                    actualEdits.Add(new TextEdit()
                    {
                        NewText = edit.NewText,
                        Range = newRange,
                    });
                }
            }

            return actualEdits.ToArray();
        }

        internal class IndentationContext
        {
            public int Line { get; set; }

            public int IndentationLevel { get; set; }

            public int ExistingIndentation { get; set; }

            public FormattingSpan FirstSpan { get; set; }

            public override string ToString()
            {
                return $"Line: {Line}, Indentation Level: {IndentationLevel}, ExistingIndentation: {ExistingIndentation}";
            }
        }
    }
}
