﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Editor.Razor;

internal static class RazorIndentationFacts
{
    // This method dives down a syntax tree looking for open curly braces, every time
    // it finds one it increments its indent until it finds the provided "line".
    //
    // Examples:
    // @{
    //    <strong>Hello World</strong>
    // }
    // Asking for desired indentation of the @{ or } lines should result in a desired indentation of 4.
    //
    // <div>
    //     @{
    //         <strong>Hello World</strong>
    //     }
    // </div>
    // Asking for desired indentation of the @{ or } lines should result in a desired indentation of 8.
    public static int? GetDesiredIndentation(
        RazorSyntaxTree syntaxTree,
        ITextSnapshot syntaxTreeSnapshot,
        ITextSnapshotLine line,
        int indentSize,
        int tabSize)
    {
        if (syntaxTree is null)
        {
            throw new ArgumentNullException(nameof(syntaxTree));
        }

        if (syntaxTreeSnapshot is null)
        {
            throw new ArgumentNullException(nameof(syntaxTreeSnapshot));
        }

        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        if (indentSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indentSize));
        }

        if (tabSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tabSize));
        }

        var previousLineEndIndex = GetPreviousLineEndIndex(syntaxTreeSnapshot, line);
        var simulatedChange = new SourceChange(previousLineEndIndex, 0, string.Empty);
        var owner = syntaxTree.Root.LocateOwner(simulatedChange);
        if (owner is null || owner.IsCodeSpanKind())
        {
            // Example,
            // @{\n
            //   ^  - The newline here is a code span and we should just let the default c# editor take care of indentation.

            return null;
        }

        int? desiredIndentation = null;
        while (owner.Parent is not null)
        {
            var children = owner.Parent.ChildNodes();
            for (var i = 0; i < children.Count; i++)
            {
                var currentChild = children[i];
                if (IsCSharpOpenCurlyBrace(currentChild))
                {
                    var lineText = line.Snapshot.GetLineFromLineNumber(currentChild.GetSourceLocation(syntaxTree.Source).LineIndex).GetText();
                    desiredIndentation = GetIndentLevelOfLine(lineText, tabSize) + indentSize;
                }

                if (currentChild == owner)
                {
                    break;
                }
            }

            if (desiredIndentation.HasValue)
            {
                return desiredIndentation;
            }

            owner = owner.Parent;
        }

        // Couldn't determine indentation
        return null;
    }

    // Internal for testing
    internal static int GetIndentLevelOfLine(string line, int tabSize)
    {
        var indentLevel = 0;

        foreach (var c in line)
        {
            if (!char.IsWhiteSpace(c))
            {
                break;
            }
            else if (c == '\t')
            {
                indentLevel += tabSize;
            }
            else
            {
                indentLevel++;
            }
        }

        return indentLevel;
    }

    // Internal for testing
    internal static int GetPreviousLineEndIndex(ITextSnapshot syntaxTreeSnapshot, ITextSnapshotLine line)
    {
        var previousLine = line.Snapshot.GetLineFromLineNumber(line.LineNumber - 1);
        var trackingPoint = previousLine.Snapshot.CreateTrackingPoint(previousLine.End, PointTrackingMode.Negative);
        var previousLineEndIndex = trackingPoint.GetPosition(syntaxTreeSnapshot);

        return previousLineEndIndex;
    }

    // Internal for testing
    internal static bool IsCSharpOpenCurlyBrace(SyntaxNode node)
    {
        var children = node.ChildNodes();

        return children.Count == 1 &&
            children[0].IsToken &&
            children[0].Kind == SyntaxKind.LeftBrace;
    }
}
