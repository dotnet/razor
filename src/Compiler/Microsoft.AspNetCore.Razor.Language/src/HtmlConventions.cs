// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

public static class HtmlConventions
{
    private static readonly char[] InvalidNonWhitespaceHtmlCharacters =
        ['@', '!', '<', '/', '?', '[', '>', ']', '=', '"', '\'', '*'];

    internal static bool IsInvalidNonWhitespaceHtmlCharacters(char testChar)
    {
        foreach (var c in InvalidNonWhitespaceHtmlCharacters)
        {
            if (c == testChar)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts from pascal/camel case to lower kebab-case.
    /// </summary>
    /// <example>
    /// SomeThing => some-thing
    /// capsONInside => caps-on-inside
    /// CAPSOnOUTSIDE => caps-on-outside
    /// ALLCAPS => allcaps
    /// One1Two2Three3 => one1-two2-three3
    /// ONE1TWO2THREE3 => one1two2three3
    /// First_Second_ThirdHi => first_second_third-hi
    /// </example>
    public static string ToHtmlCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var input = name.AsSpan();

        using var _ = StringBuilderPool.GetPooledObject(out var result);
        result.SetCapacityIfLarger(input.Length + 5);

        // It's slightly faster to use a foreach and index manually, than to use a for 🤷‍
        var i = 0;
        foreach (var c in name)
        {
            if (char.IsUpper(c))
            {
                // Insert hyphen if:
                // - Not the first character, and
                // - Previous is lowercase or digit, or
                // - Previous is uppercase and next is lowercase (e.g. CAPSOn → caps-on)
                if (i > 0)
                {
                    var prev = input[i - 1];
                    var prevIsLowerOrDigit = char.IsLower(prev) || char.IsDigit(prev);
                    var prevIsUpper = char.IsUpper(prev);
                    var nextIsLower = (i + 1 < input.Length) && char.IsLower(input[i + 1]);
                    if (prevIsLowerOrDigit || (prevIsUpper && nextIsLower))
                    {
                        result.Append('-');
                    }
                }
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }

            i++;
        }

        return result.ToString();
    }
}
