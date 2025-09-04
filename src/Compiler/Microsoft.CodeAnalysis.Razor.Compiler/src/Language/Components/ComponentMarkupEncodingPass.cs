// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Legacy;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal class ComponentMarkupEncodingPass : ComponentIntermediateNodePassBase, IRazorOptimizationPass
{
    private readonly RazorLanguageVersion _version;

    public ComponentMarkupEncodingPass(RazorLanguageVersion version)
    {
        _version = version;
    }

    // Runs after ComponentMarkupBlockPass
    public override int Order => ComponentMarkupDiagnosticPass.DefaultOrder + 20;

    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        if (!IsComponentDocument(documentNode))
        {
            return;
        }

        if (documentNode.Options.DesignTime)
        {
            // Nothing to do during design time.
            return;
        }

        var rewriter = new Rewriter(_version);
        rewriter.Visit(documentNode);
    }

    private class Rewriter : IntermediateNodeWalker
    {
        // Markup content in components are rendered in one of the following two ways,
        // AddContent - we encode it when used with prerendering and inserted into the DOM in a safe way (low perf impact)
        // AddMarkupContent - renders the content directly as markup (high perf impact)
        // Because of this, we want to use AddContent as much as possible.
        //
        // We want to use AddMarkupContent to avoid aggressive encoding during prerendering.
        // Specifically, when one of the following characters are in the content,
        // 1. New lines (\r, \n), tabs (\t), angle brackets (<, >) - so they get rendered as actual new lines, tabs, brackets instead of &#xA;
        // 2. Any character outside the ASCII range

        private static readonly char[] EncodedCharacters = new[] { '\r', '\n', '\t', '<', '>' };

        private readonly bool _avoidEncodingScripts;

        private bool _avoidEncodingContent;

        public Rewriter(RazorLanguageVersion version)
        {
            _avoidEncodingScripts = version >= RazorLanguageVersion.Version_8_0;
        }

        public override void VisitMarkupElement(MarkupElementIntermediateNode node)
        {
            // We don't want to HTML-encode literal content inside <script> tags.
            var oldAvoidEncodingContent = _avoidEncodingContent;
            _avoidEncodingContent = _avoidEncodingContent || (
                _avoidEncodingScripts &&
                string.Equals("script", node.TagName, StringComparison.OrdinalIgnoreCase));

            base.VisitMarkupElement(node);

            _avoidEncodingContent = oldAvoidEncodingContent;
        }

        public override void VisitHtml(HtmlContentIntermediateNode node)
        {
            if (_avoidEncodingContent)
            {
                node.HasEncodedContent = true;
                return;
            }

            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child is not HtmlIntermediateToken token || string.IsNullOrEmpty(token.Content))
                {
                    // We only care about Html tokens.
                    continue;
                }

                for (var j = 0; j < token.Content.Length; j++)
                {
                    var ch = token.Content[j];
                    // ASCII range is 0 - 127
                    if (ch > 127 || EncodedCharacters.Contains(ch))
                    {
                        node.HasEncodedContent = true;
                        return;
                    }
                }
            }

            // If we reach here, we don't have newlines, tabs or non-ascii characters in this node.
            // If we can successfully decode all HTML entities(if any) in this node, we can safely let it call AddContent.
            var decodedContent = new string[node.Children.Count];
            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child is not HtmlIntermediateToken token || string.IsNullOrEmpty(token.Content))
                {
                    // We only care about Html tokens.
                    continue;
                }

                if (TryDecodeHtmlEntities(token.Content.AsMemory(), out var decoded))
                {
                    decodedContent[i] = decoded;
                }
                else
                {
                    node.HasEncodedContent = true;
                    return;
                }
            }

            // If we reach here, it means we have successfully decoded all content.
            // Replace all token content with the decoded value.
            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child is not IntermediateToken token || string.IsNullOrEmpty(token.Content))
                {
                    // We only care about Html tokens.
                    continue;
                }

                token.UpdateContent(decodedContent[i]);
            }
        }

        private static bool TryDecodeHtmlEntities(ReadOnlyMemory<char> content, out string decoded)
        {
            decoded = null;

            if (content.IsEmpty)
            {
                decoded = string.Empty;
                return true;
            }

            decoded = string.TryBuild(content, static (ref builder, content) =>
            {
                while (!content.IsEmpty)
                {
                    var ampersandIndex = content.Span.IndexOf('&');

                    if (ampersandIndex == -1)
                    {
                        // No more entities, add the remaining content
                        builder.Append(content);
                        break;
                    }

                    if (!TryGetHtmlEntity(content[ampersandIndex..], out var entity, out var replacement))
                    {
                        // We found a '&' that we don't know what to do with. Don't try to decode further.
                        return false;
                    }

                    // We found a valid entity.
                    // First, add the text before the entity.
                    // Then, add the replacement text for the entity.
                    if (ampersandIndex > 0)
                    {
                        builder.Append(content[..ampersandIndex]);
                    }

                    builder.Append(replacement.AsMemory());

                    // Skip past the processed entity and continue.
                    content = content[(ampersandIndex + entity.Length)..];
                }

                Debug.Assert(builder.Length > 0, "How could builder be empty if content was not?");

                return true;
            });

            return decoded is not null;
        }

        private static bool TryGetHtmlEntity(ReadOnlyMemory<char> content, out ReadOnlyMemory<char> entity, out string replacement)
        {
            // We're at '&'. Check if it is the start of an HTML entity.
            entity = default;
            replacement = null;

            var span = content.Span;

            for (var i = 1; i < span.Length; i++)
            {
                var ch = span[i];

                if (char.IsLetterOrDigit(ch) || ch == '#')
                {
                    continue;
                }

                if (ch == ';')
                {
                    // Found the end of an entity. +1 to include the ';'
                    entity = content[0..(i + 1)];
                    break;
                }

                break;
            }

            if (!entity.IsEmpty)
            {
                if (entity.Span.StartsWith("&#".AsSpan()))
                {
                    // Extract the codepoint and map it to an entity.

                    // entity is guaranteed to be of the format '&#****;'
                    var digitsSpan = entity.Span[2..^1];
                    var style = NumberStyles.Integer;

                    switch (digitsSpan)
                    {
                        case ['x' or 'X', .. var rest]: // &#x41; or &#X41;
                            style = NumberStyles.HexNumber;
                            digitsSpan = rest;
                            break;

                        case ['0', 'x' or 'X', .. var rest]: // &#0x41; or &#0X41; (Technically illegal but supported by Razor)
                            style = NumberStyles.HexNumber;
                            digitsSpan = rest;
                            break;
                    }

#if NET
                    var success = int.TryParse(digitsSpan, style, CultureInfo.InvariantCulture, out var codePoint);
#else
                    // Sadly, we have to allocate on non-.NET to call int.TryParse.
                    var success = int.TryParse(digitsSpan.ToString(), style, CultureInfo.InvariantCulture, out var codePoint);
#endif

                    if (success)
                    {
                        // First try the special HTML entity code points dictionary
                        if (ParserHelpers.HtmlEntityCodePoints.TryGetValue(codePoint, out replacement))
                        {
                            return true;
                        }

                        // For basic printable Unicode characters, convert directly
                        // Use a conservative range that matches typical browser behavior:
                        // - Start at 0x20 (space) to exclude control characters
                        // - End at 0xFFFF to stay within Basic Multilingual Plane
                        // - Exclude surrogate pair range 0xD800-0xDFFF
                        if (codePoint >= 0x20 && codePoint <= 0xFFFF &&
                            (codePoint < 0xD800 || codePoint > 0xDFFF))
                        {
                            replacement = char.ConvertFromUtf32(codePoint);
                            return true;
                        }
                    }
                }

#if NET9_0_OR_GREATER
                if (ParserHelpers.NamedHtmlEntities.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(entity.Span, out replacement))
                {
                    return true;
                }
#else
                if (ParserHelpers.NamedHtmlEntities.TryGetValue(entity.ToString(), out replacement))
                {
                    return true;
                }
#endif

                // Found ';' but entity is not recognized
                entity = default;
            }

            // The '&' is not part of an HTML entity.
            return false;
        }
    }
}
