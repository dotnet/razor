﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;

public abstract class RazorOnAutoInsertProviderTestBase : LanguageServerTestBase
{
    protected RazorOnAutoInsertProviderTestBase(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    internal abstract IOnAutoInsertProvider CreateProvider();

    protected void RunAutoInsertTest(string input, string expected, int tabSize = 4, bool insertSpaces = true, string fileKind = default, IReadOnlyList<TagHelperDescriptor> tagHelpers = default)
    {
        // Arrange
        TestFileMarkupParser.GetPosition(input, out input, out var location);

        var source = SourceText.From(input);
        source.GetLineAndOffset(location, out var line, out var column);
        var position = new Position(line, column);

        var path = "file:///path/to/document.razor";
        var uri = new Uri(path);
        var codeDocument = CreateCodeDocument(source, uri.AbsolutePath, tagHelpers, fileKind: fileKind);
        var options = new FormattingOptions()
        {
            TabSize = tabSize,
            InsertSpaces = insertSpaces,
        };

        var provider = CreateProvider();
        var context = FormattingContext.Create(uri, Mock.Of<IDocumentSnapshot>(MockBehavior.Strict), codeDocument, options, TestAdhocWorkspaceFactory.Instance);

        // Act
        if (!provider.TryResolveInsertion(position, context, out var edit, out _))
        {
            edit = null;
        }

        // Assert
        var edited = edit is null ? source : ApplyEdit(source, edit);
        var actual = edited.ToString();
        Assert.Equal(expected, actual);
    }

    private static SourceText ApplyEdit(SourceText source, TextEdit edit)
    {
        var change = edit.ToTextChange(source);
        return source.WithChanges(change);
    }

    private static RazorCodeDocument CreateCodeDocument(SourceText text, string path, IReadOnlyList<TagHelperDescriptor> tagHelpers = null, string fileKind = default)
    {
        fileKind ??= FileKinds.Component;
        tagHelpers ??= Array.Empty<TagHelperDescriptor>();
        var sourceDocument = text.GetRazorSourceDocument(path, path);
        var projectEngine = RazorProjectEngine.Create(builder => { });
        var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, fileKind, Array.Empty<RazorSourceDocument>(), tagHelpers);
        return codeDocument;
    }
}
