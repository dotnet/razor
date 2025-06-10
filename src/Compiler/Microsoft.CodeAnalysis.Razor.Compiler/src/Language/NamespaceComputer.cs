// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Text;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

internal static class NamespaceComputer
{
    private static ReadOnlySpan<char> PathSeparators => ['/', '\\'];
    private static ReadOnlySpan<char> NamespaceSeparators => ['.'];

    public static bool TryComputeNamespace(RazorCodeDocument codeDocument, bool fallbackToRootNamespace, bool considerImports, out string @namespace, out SourceSpan? namespaceSpan)
    {
        var filePath = codeDocument.Source.FilePath;
        if (filePath == null || codeDocument.Source.RelativePath == null || filePath.Length < codeDocument.Source.RelativePath.Length)
        {
            @namespace = null;
            namespaceSpan = null;
            return false;
        }

        // If the document or it's imports contains a @namespace directive, we want to use that over the root namespace.
        string lastNamespaceContent = null;
        namespaceSpan = null;

        if (considerImports && codeDocument.TryGetImportSyntaxTrees(out var importSyntaxTrees))
        {
            // ImportSyntaxTrees is usually set. Just being defensive.
            foreach (var importSyntaxTree in importSyntaxTrees)
            {
                if (importSyntaxTree != null && NamespaceVisitor.TryGetLastNamespaceDirective(importSyntaxTree, out var importNamespaceContent, out var importNamespaceLocation))
                {
                    lastNamespaceContent = importNamespaceContent;
                    namespaceSpan = importNamespaceLocation;
                }
            }
        }

        if (codeDocument.TryGetSyntaxTree(out var syntaxTree) &&
            NamespaceVisitor.TryGetLastNamespaceDirective(syntaxTree, out var namespaceContent, out var namespaceLocation))
        {
            lastNamespaceContent = namespaceContent;
            namespaceSpan = namespaceLocation;
        }

        var relativePath = codeDocument.Source.RelativePath.AsSpan();

        // If there are multiple @namespace directives in the hierarchy,
        // we want to pick the closest one to the current document.
        if (!string.IsNullOrEmpty(lastNamespaceContent))
        {
            using var _ = StringBuilderPool.GetPooledObject(out var builder);

            AppendNamespace(lastNamespaceContent, builder);

            var sourceFilePath = codeDocument.Source.FilePath.AsSpan();
            var directiveDirectorySpan = NormalizeDirectory(namespaceSpan.Value.FilePath);

            // We're specifically using OrdinalIgnoreCase here because Razor treats all paths as case-insensitive.
            if (sourceFilePath.Length > directiveDirectorySpan.Length &&
                sourceFilePath.StartsWith(directiveDirectorySpan, StringComparison.OrdinalIgnoreCase))
            {
                // We know that the document containing the namespace directive is in the current document's hierarchy.
                // Let's compute the actual relative path that we'll use to compute the namespace suffix.
                relativePath = sourceFilePath[directiveDirectorySpan.Length..];

                AppendRelativePath(relativePath, builder);
            }

            @namespace = builder.ToString();
            return true;
        }

        if (fallbackToRootNamespace)
        {
            using var _ = StringBuilderPool.GetPooledObject(out var builder);

            var rootNamespace = codeDocument.CodeGenerationOptions.RootNamespace;

            if (!rootNamespace.IsNullOrEmpty() || codeDocument.FileKind.IsComponent())
            {
                AppendNamespace(rootNamespace, builder);
                AppendRelativePath(relativePath, builder);

                @namespace = builder.ToString();
                namespaceSpan = null;
                return true;
            }
        }

        // There was no valid @namespace directive.
        @namespace = null;
        return false;
    }

    // We want to normalize the path of the file containing the '@namespace' directive to just the containing
    // directory with a trailing separator.
    //
    // Not using Path.GetDirectoryName here because it doesn't meet these requirements, and we want to handle
    // both 'view engine' style paths and absolute paths.
    //
    // We also don't normalize the separators here. We expect that all documents are using a consistent style of path.
    //
    // If we can't normalize the path, we just return null so it will be ignored.
    private static ReadOnlySpan<char> NormalizeDirectory(string path)
    {
        var span = path.AsSpanOrDefault();

        if (span.IsEmpty)
        {
            return default;
        }

        var lastSeparator = span.LastIndexOfAny(PathSeparators);
        if (lastSeparator < 0)
        {
            return default;
        }

        // Includes the separator
        return span[..(lastSeparator + 1)];
    }

    private static void AppendNamespace(string namespaceName, StringBuilder builder)
    {
        var tokenizer = new StringTokenizer(namespaceName, NamespaceSeparators);
        var first = true;

        foreach (var token in tokenizer)
        {
            if (token.IsEmpty)
            {
                continue;
            }

            if (first)
            {
                first = false;
            }
            else
            {
                builder.Append('.');
            }

            CSharpIdentifier.AppendSanitized(builder, token);
        }
    }

    private static void AppendRelativePath(ReadOnlySpan<char> relativePath, StringBuilder builder)
    {
        var lastSeparatorIndex = relativePath.LastIndexOfAny(PathSeparators);
        if (lastSeparatorIndex < 0)
        {
            return;
        }

        relativePath = relativePath[..lastSeparatorIndex];

        var tokenizer = new StringTokenizer(relativePath, PathSeparators);

        foreach (var token in tokenizer)
        {
            if (token.IsEmpty)
            {
                continue;
            }

            if (builder.Length != 0)
            {
                builder.Append('.');
            }

            CSharpIdentifier.AppendSanitized(builder, token);
        }
    }

    private class NamespaceVisitor : SyntaxWalker
    {
        private readonly RazorSourceDocument _source;

        private NamespaceVisitor(RazorSourceDocument source)
        {
            _source = source;
        }

        public string LastNamespaceContent { get; set; }

        public SourceSpan LastNamespaceLocation { get; set; }

        public static bool TryGetLastNamespaceDirective(
            RazorSyntaxTree syntaxTree,
            out string namespaceDirectiveContent,
            out SourceSpan namespaceDirectiveSpan)
        {
            var visitor = new NamespaceVisitor(syntaxTree.Source);
            visitor.Visit(syntaxTree.Root);
            if (string.IsNullOrEmpty(visitor.LastNamespaceContent))
            {
                namespaceDirectiveContent = null;
                namespaceDirectiveSpan = SourceSpan.Undefined;
                return false;
            }

            namespaceDirectiveContent = visitor.LastNamespaceContent;
            namespaceDirectiveSpan = visitor.LastNamespaceLocation;
            return true;
        }

        public override void VisitRazorDirective(RazorDirectiveSyntax node)
        {
            if (node != null && node.DirectiveDescriptor == NamespaceDirective.Directive)
            {
                if (node.Body is RazorDirectiveBodySyntax { CSharpCode.Children: [_, CSharpSyntaxNode @namespace, ..] })
                {
                    LastNamespaceContent = @namespace.GetContent();
                    LastNamespaceLocation = @namespace.GetSourceSpan(_source);
                }
            }

            base.VisitRazorDirective(node);
        }
    }
}
