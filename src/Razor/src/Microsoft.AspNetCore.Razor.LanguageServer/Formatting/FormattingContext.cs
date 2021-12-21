// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class FormattingContext : IDisposable
    {
        private readonly AdhocWorkspaceFactory _workspaceFactory;
        private Document? _csharpWorkspaceDocument;
        private AdhocWorkspace? _csharpWorkspace;

        private IReadOnlyList<FormattingSpan>? _formattingSpans;

        private FormattingContext(AdhocWorkspaceFactory workspaceFactory)
        {
            _workspaceFactory = workspaceFactory;
        }

        public static bool SkipValidateComponents { get; set; }

        public DocumentUri Uri { get; private set; } = null!;

        public DocumentSnapshot OriginalSnapshot { get; private set; } = null!;

        public RazorCodeDocument CodeDocument { get; private set; } = null!;

        public SourceText SourceText => CodeDocument.GetSourceText();

        public SourceText CSharpSourceText => CodeDocument.GetCSharpSourceText();

        public Document CSharpWorkspaceDocument
        {
            get
            {
                if (_csharpWorkspaceDocument is null)
                {
                    var workspace = CSharpWorkspace;
                    var project = workspace.AddProject("TestProject", LanguageNames.CSharp);
                    var csharpSourceText = CodeDocument.GetCSharpSourceText();
                    _csharpWorkspaceDocument = workspace.AddDocument(project.Id, "TestDocument", csharpSourceText);
                }

                return _csharpWorkspaceDocument;
            }
        }

        public AdhocWorkspace CSharpWorkspace
        {
            get
            {
                if (_csharpWorkspace is null)
                {
                    var adhocWorkspace = _workspaceFactory.Create();
                    var csharpOptions = adhocWorkspace.Options
                        .WithChangedOption(CodeAnalysis.Formatting.FormattingOptions.TabSize, LanguageNames.CSharp, (int)Options.TabSize)
                        .WithChangedOption(CodeAnalysis.Formatting.FormattingOptions.IndentationSize, LanguageNames.CSharp, (int)Options.TabSize)
                        .WithChangedOption(CodeAnalysis.Formatting.FormattingOptions.UseTabs, LanguageNames.CSharp, !Options.InsertSpaces);
                    adhocWorkspace.TryApplyChanges(adhocWorkspace.CurrentSolution.WithOptions(csharpOptions));
                    _csharpWorkspace = adhocWorkspace;
                }

                return _csharpWorkspace;
            }
        }

        public FormattingOptions Options { get; private set; } = null!;

        public string NewLineString => Environment.NewLine;

        public bool IsFormatOnType { get; private set; }

        public bool AutomaticallyAddUsings { get; private set; }

        public Range Range { get; private set; } = null!;

        /// <summary>A Dictionary of int (line number) to IndentationContext.</summary>
        /// <remarks>
        /// Don't use this to discover the indentation level you should have, use
        /// <see cref="TryGetIndentationLevel(int, out int)"/> which operates on the position rather than just the line.
        /// </remarks>
        public IReadOnlyDictionary<int, IndentationContext> Indentations { get; private set; } = null!;

        public IReadOnlyList<FormattingSpan> FormattingSpans
        {
            get
            {
                if (_formattingSpans is null)
                {
                    var syntaxTree = CodeDocument.GetSyntaxTree();
                    _formattingSpans = syntaxTree.GetFormattingSpans();
                }

                return _formattingSpans;
            }
        }

        /// <summary>
        /// Generates a string of indentation based on a specific indentation level. For instance, inside of a C# method represents 1 indentation level. A method within a class would have indentaiton level of 2 by default etc.
        /// </summary>
        /// <param name="indentationLevel">The indentation level to represent</param>
        /// <returns>A whitespace string representing the indentation level based on the configuration.</returns>
        public string GetIndentationLevelString(int indentationLevel)
        {
            var indentation = GetIndentationOffsetForLevel(indentationLevel);
            var indentationString = GetIndentationString(indentation);
            return indentationString;
        }

        /// <summary>
        /// Given a <paramref name="indentation"/> amount of characters, generate a string representing the configured indentation.
        /// </summary>
        /// <param name="indentation">An amount of characters to represent the indentation</param>
        /// <returns>A whitespace string representation indentation.</returns>
        public string GetIndentationString(int indentation)
        {
            if (Options.InsertSpaces)
            {
                return new string(' ', indentation);
            }
            else
            {
                var tabs = indentation / Options.TabSize;
                var tabPrefix = new string('\t', (int)tabs);

                var spaces = indentation % Options.TabSize;
                var spaceSuffix = new string(' ', (int)spaces);

                var combined = string.Concat(tabPrefix, spaceSuffix);
                return combined;
            }
        }

        /// <summary>
        /// Given an offset return the corresponding indent level.
        /// </summary>
        /// <param name="offset">A value represents the number of spaces/tabs at the start of a line.</param>
        /// <returns>The corresponding indent level.</returns>
        public int GetIndentationLevelForOffset(int offset)
        {
            if (Options.InsertSpaces)
            {
                offset /= (int)Options.TabSize;
            }

            return offset;
        }

        /// <summary>
        /// Given a level, returns the corresponding offset.
        /// </summary>
        /// <param name="level">A value representing the indentation level.</param>
        /// <returns></returns>
        public int GetIndentationOffsetForLevel(int level)
        {
            return level * (int)Options.TabSize;
        }

        public bool TryGetIndentationLevel(int position, out int indentationLevel)
        {
            if (TryGetFormattingSpan(position, out var span))
            {
                indentationLevel = span.IndentationLevel;
                return true;
            }

            indentationLevel = 0;
            return false;
        }

        public bool TryGetFormattingSpan(int absoluteIndex, [NotNullWhen(true)] out FormattingSpan? result)
        {
            result = null;
            for (var i = 0; i < FormattingSpans.Count; i++)
            {
                var formattingspan = FormattingSpans[i];
                var span = formattingspan.Span;

                if (span.Start <= absoluteIndex && span.End >= absoluteIndex)
                {
                    if (span.End == absoluteIndex && span.Length > 0)
                    {
                        // We're at an edge.
                        // Non-marker spans (spans.length == 0) do not own the edges after it
                        continue;
                    }

                    result = formattingspan;
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            _csharpWorkspace?.Dispose();
            if (_csharpWorkspaceDocument != null)
            {
                _csharpWorkspaceDocument = null;
            }
        }

        public async Task<FormattingContext> WithTextAsync(SourceText changedText)
        {
            if (changedText is null)
            {
                throw new ArgumentNullException(nameof(changedText));
            }

            var engine = OriginalSnapshot.Project.GetProjectEngine();
            var importSources = new List<RazorSourceDocument>();

            var imports = OriginalSnapshot.GetImports();
            foreach (var import in imports)
            {
                var sourceText = await import.GetTextAsync();
                var source = sourceText.GetRazorSourceDocument(import.FilePath, import.TargetPath);
                importSources.Add(source);
            }

            var changedSourceDocument = changedText.GetRazorSourceDocument(OriginalSnapshot.FilePath, OriginalSnapshot.TargetPath);

            var codeDocument = engine.ProcessDesignTime(changedSourceDocument, OriginalSnapshot.FileKind, importSources, OriginalSnapshot.Project.TagHelpers);

            ValidateComponents(CodeDocument, codeDocument);

            var newContext = Create(Uri, OriginalSnapshot, codeDocument, Options, _workspaceFactory, IsFormatOnType, AutomaticallyAddUsings);
            return newContext;
        }

        /// <summary>
        /// It can be difficult in the testing infrastructure to correct constructs input files that work consistently across
        /// context changes, so this method validates that the number of components isn't changing due to lost tag help info.
        /// Without this guarantee its hard to reason about test behaviour/failures.
        /// </summary>
        [Conditional("DEBUG")]
        private static void ValidateComponents(RazorCodeDocument oldCodeDocument, RazorCodeDocument newCodeDocument)
        {
            if (SkipValidateComponents)
            {
                return;
            }

            var oldTagHelperElements = oldCodeDocument.GetSyntaxTree().Root.DescendantNodesAndSelf().OfType<Language.Syntax.MarkupTagHelperElementSyntax>().Count();
            var newTagHelperElements = newCodeDocument.GetSyntaxTree().Root.DescendantNodesAndSelf().OfType<Language.Syntax.MarkupTagHelperElementSyntax>().Count();
            Debug.Assert(oldTagHelperElements == newTagHelperElements, $"Previous context had {oldTagHelperElements} components, new only has {newTagHelperElements}.");
        }

        public static FormattingContext Create(
            DocumentUri uri,
            DocumentSnapshot originalSnapshot,
            RazorCodeDocument codeDocument,
            FormattingOptions options,
            AdhocWorkspaceFactory workspaceFactory,
            bool isFormatOnType,
            bool automaticallyAddUsings)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (originalSnapshot is null)
            {
                throw new ArgumentNullException(nameof(originalSnapshot));
            }

            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (workspaceFactory is null)
            {
                throw new ArgumentNullException(nameof(workspaceFactory));
            }

            var result = new FormattingContext(workspaceFactory)
            {
                Uri = uri,
                OriginalSnapshot = originalSnapshot,
                CodeDocument = codeDocument,
                Options = options,
                IsFormatOnType = isFormatOnType,
                AutomaticallyAddUsings = automaticallyAddUsings
            };

            var sourceText = codeDocument.GetSourceText();
            var indentations = new Dictionary<int, IndentationContext>();

            var previousIndentationLevel = 0;
            for (var i = 0; i < sourceText.Lines.Count; i++)
            {
                // Get first non-whitespace character position
                var nonWsPos = sourceText.Lines[i].GetFirstNonWhitespacePosition();
                var existingIndentation = (nonWsPos ?? sourceText.Lines[i].End) - sourceText.Lines[i].Start;

                // The existingIndentation above is measured in characters, and is used to create text edits
                // The below is measured in columns, so takes into account tab size. This is useful for creating
                // new indentation strings
                var existingIndentationSize = sourceText.Lines[i].GetIndentationSize(options.TabSize);

                var emptyOrWhitespaceLine = false;
                if (nonWsPos is null)
                {
                    emptyOrWhitespaceLine = true;
                    nonWsPos = sourceText.Lines[i].Start;
                }

                // position now contains the first non-whitespace character or 0. Get the corresponding FormattingSpan.
                if (result.TryGetFormattingSpan(nonWsPos.Value, out var span))
                {
                    indentations[i] = new IndentationContext(firstSpan: span)
                    {
                        Line = i,
                        RazorIndentationLevel = span.RazorIndentationLevel,
                        HtmlIndentationLevel = span.HtmlIndentationLevel,
                        RelativeIndentationLevel = span.IndentationLevel - previousIndentationLevel,
                        ExistingIndentation = existingIndentation,
                        ExistingIndentationSize = existingIndentationSize,
                        EmptyOrWhitespaceLine = emptyOrWhitespaceLine,
                    };
                    previousIndentationLevel = span.IndentationLevel;
                }
                else
                {
                    // Couldn't find a corresponding FormattingSpan. Happens if it is a 0 length line.
                    // Let's create a 0 length span to represent this and default it to HTML.
                    var placeholderSpan = new FormattingSpan(
                        new Language.Syntax.TextSpan(nonWsPos.Value, 0),
                        new Language.Syntax.TextSpan(nonWsPos.Value, 0),
                        FormattingSpanKind.Markup,
                        FormattingBlockKind.Markup,
                        razorIndentationLevel: 0,
                        htmlIndentationLevel: 0,
                        isInClassBody: false,
                        componentLambdaNestingLevel: 0);

                    indentations[i] = new IndentationContext(firstSpan: placeholderSpan)
                    {
                        Line = i,
                        RazorIndentationLevel = 0,
                        HtmlIndentationLevel = 0,
                        RelativeIndentationLevel = previousIndentationLevel,
                        ExistingIndentation = existingIndentation,
                        ExistingIndentationSize = existingIndentation,
                        EmptyOrWhitespaceLine = emptyOrWhitespaceLine,
                    };
                }
            }

            result.Indentations = indentations;

            return result;
        }
    }
}
