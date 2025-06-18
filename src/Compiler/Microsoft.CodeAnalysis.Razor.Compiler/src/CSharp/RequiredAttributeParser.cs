// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor;

internal sealed class RequiredAttributeParser
{
    private const char RequiredAttributeWildcardSuffix = '*';

    private static readonly FrozenDictionary<char, RequiredAttributeValueComparison> s_cssValueComparisons =
        new Dictionary<char, RequiredAttributeValueComparison>
        {
            ['='] = RequiredAttributeValueComparison.FullMatch,
            ['^'] = RequiredAttributeValueComparison.PrefixMatch,
            ['$'] = RequiredAttributeValueComparison.SuffixMatch
        }.ToFrozenDictionary();

    private static readonly char[] s_invalidPlainAttributeNameCharacters = [' ', '\t', ',', RequiredAttributeWildcardSuffix];
    private static readonly char[] s_invalidCssAttributeNameCharacters = [' ', '\t', ',', ']', .. s_cssValueComparisons.Keys];
    private static readonly char[] s_invalidCssQuotelessValueCharacters = [' ', '\t', ']'];

    private readonly string _requiredAttributes;
    private int _index;

    private RequiredAttributeParser(string requiredAttributes)
    {
        _requiredAttributes = requiredAttributes;
    }

    public static void AddRequiredAttributes(string requiredAttributes, TagMatchingRuleDescriptorBuilder ruleBuilder)
    {
        var parser = new RequiredAttributeParser(requiredAttributes);
        parser.AddRequiredAttributes(ruleBuilder);
    }

    private char Current => _requiredAttributes[_index];

    private bool AtEnd => _index >= _requiredAttributes.Length;

    public void AddRequiredAttributes(TagMatchingRuleDescriptorBuilder ruleBuilder)
    {
        if (_requiredAttributes.IsNullOrEmpty())
        {
            return;
        }

        PassOptionalWhitespace();

        do
        {
            var successfulParse = true;

            ruleBuilder.Attribute(attributeBuilder =>
            {
                if (At('['))
                {
                    if (!TryParseCssSelector(attributeBuilder))
                    {
                        successfulParse = false;
                        return;
                    }
                }
                else
                {
                    ParsePlainSelector(attributeBuilder);
                }

                PassOptionalWhitespace();

                if (At(','))
                {
                    _index++;

                    if (!EnsureNotAtEnd(attributeBuilder))
                    {
                        successfulParse = false;
                        return;
                    }
                }
                else if (!AtEnd)
                {
                    var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidRequiredAttributeCharacter(Current, _requiredAttributes);
                    attributeBuilder.Diagnostics.Add(diagnostic);
                    successfulParse = false;
                    return;
                }

                PassOptionalWhitespace();
            });

            if (!successfulParse)
            {
                break;
            }
        }
        while (!AtEnd);
    }

    private void ParsePlainSelector(RequiredAttributeDescriptorBuilder attributeBuilder)
    {
        var nameEndIndex = _requiredAttributes.IndexOfAny(s_invalidPlainAttributeNameCharacters, _index);

        string attributeName;
        var nameComparison = RequiredAttributeNameComparison.FullMatch;

        if (nameEndIndex == -1)
        {
            attributeName = _requiredAttributes[_index..];
            _index = _requiredAttributes.Length;
        }
        else
        {
            attributeName = _requiredAttributes[_index..nameEndIndex];
            _index = nameEndIndex;

            if (_requiredAttributes[nameEndIndex] == RequiredAttributeWildcardSuffix)
            {
                nameComparison = RequiredAttributeNameComparison.PrefixMatch;

                // Move past wild card
                _index++;
            }
        }

        attributeBuilder.Name = attributeName;
        attributeBuilder.NameComparison = nameComparison;
    }

    private void ParseCssAttributeName(RequiredAttributeDescriptorBuilder builder)
    {
        var nameStartIndex = _index;
        var nameEndIndex = _requiredAttributes.IndexOfAny(s_invalidCssAttributeNameCharacters, _index);

        if (nameEndIndex == -1)
        {
            nameEndIndex = _requiredAttributes.Length;
        }

        _index = nameEndIndex;

        builder.Name = _requiredAttributes[nameStartIndex..nameEndIndex];
    }

    private bool TryParseCssValueComparison(RequiredAttributeDescriptorBuilder builder, out RequiredAttributeValueComparison valueComparison)
    {
        Debug.Assert(!AtEnd);

        if (s_cssValueComparisons.TryGetValue(Current, out valueComparison))
        {
            var op = Current;
            _index++;

            if (op != '=' && At('='))
            {
                // Two length operator (ex: ^=). Move past the second piece
                _index++;
            }
            else if (op != '=') // We're at an incomplete operator (ex: [foo^]
            {
                var diagnostic = RazorDiagnosticFactory.CreateTagHelper_PartialRequiredAttributeOperator(op, _requiredAttributes);
                builder.Diagnostics.Add(diagnostic);

                return false;
            }
        }
        else if (!At(']'))
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidRequiredAttributeOperator(Current, _requiredAttributes);
            builder.Diagnostics.Add(diagnostic);

            return false;
        }

        builder.ValueComparison = valueComparison;

        return true;
    }

    private bool TryParseCssValue(RequiredAttributeDescriptorBuilder builder)
    {
        int valueStart, valueEnd;

        if (At('\'') || At('"'))
        {
            var quote = Current;

            // Move past the quote
            _index++;

            valueStart = _index;
            valueEnd = _requiredAttributes.IndexOf(quote, _index);
            if (valueEnd == -1)
            {
                var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidRequiredAttributeMismatchedQuotes(quote, _requiredAttributes);
                builder.Diagnostics.Add(diagnostic);

                return false;
            }

            _index = valueEnd + 1;
        }
        else
        {
            valueStart = _index;
            var valueEndIndex = _requiredAttributes.IndexOfAny(s_invalidCssQuotelessValueCharacters, _index);
            valueEnd = valueEndIndex == -1 ? _requiredAttributes.Length : valueEndIndex;
            _index = valueEnd;
        }

        builder.Value = _requiredAttributes[valueStart..valueEnd];

        return true;
    }

    private bool TryParseCssSelector(RequiredAttributeDescriptorBuilder attributeBuilder)
    {
        Debug.Assert(At('['));

        // Move past '['.
        _index++;
        PassOptionalWhitespace();

        ParseCssAttributeName(attributeBuilder);

        PassOptionalWhitespace();

        if (!EnsureNotAtEnd(attributeBuilder))
        {
            return false;
        }

        if (!TryParseCssValueComparison(attributeBuilder, out RequiredAttributeValueComparison valueComparison))
        {
            return false;
        }

        PassOptionalWhitespace();

        if (!EnsureNotAtEnd(attributeBuilder))
        {
            return false;
        }

        if (valueComparison != RequiredAttributeValueComparison.None && !TryParseCssValue(attributeBuilder))
        {
            return false;
        }

        PassOptionalWhitespace();

        if (At(']'))
        {
            // Move past the ending bracket.
            _index++;
            return true;
        }
        else if (AtEnd)
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_CouldNotFindMatchingEndBrace(_requiredAttributes);
            attributeBuilder.Diagnostics.Add(diagnostic);
        }
        else
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidRequiredAttributeCharacter(Current, _requiredAttributes);
            attributeBuilder.Diagnostics.Add(diagnostic);
        }

        return false;
    }

    private bool EnsureNotAtEnd(RequiredAttributeDescriptorBuilder builder)
    {
        if (AtEnd)
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_CouldNotFindMatchingEndBrace(_requiredAttributes);
            builder.Diagnostics.Add(diagnostic);

            return false;
        }

        return true;
    }

    private bool At(char c)
    {
        return !AtEnd && Current == c;
    }

    private void PassOptionalWhitespace()
    {
        while (!AtEnd && (Current == ' ' || Current == '\t'))
        {
            _index++;
        }
    }
}
