// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace BROKEN;

internal static class VSInternalCodeActionExtensions
{
    private const string RazorNameString = "_razorName";

    public static string GetName(this VSInternalCodeAction codeAction)
    {
        var dict = GetData(codeAction);
        var name = dict?[RazorNameString].FirstOrDefault();

        if (name is null)
        {
            throw new InvalidOperationException("The CodeAction did not have an expected name");
        }

        return name;
    }

    public static bool SetName(this VSInternalCodeAction codeAction, ImmutableHashSet<string?> allAvailableCodeActionNames)
    {
        const string CustomTagName = "CustomTags";
        var dict = GetData(codeAction);
        if (dict is null)
        {
            return false;
        }

        var tags = dict[CustomTagName];

        if (tags is null || tags.Length == 0)
        {
            dict[RazorNameString] = null;
            return false;
        }

        foreach (var tag in tags)
        {
            if (allAvailableCodeActionNames.Contains(tag))
            {
                dict[RazorNameString] = new string[] { tag };
                return true;
            }
        }

        return false;
    }

    private static IDictionary<string, string[]?>? GetData(VSInternalCodeAction codeAction)
    {
        return codeAction.Data as IDictionary<string, string[]?>;
    }
}
