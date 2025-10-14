// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

internal sealed partial class MarkupMinimizedTagHelperDirectiveAttributeSyntax
{
    public string FullName
    {
        get
        {
            return field ??= string.Build(AppendContent);

            void AppendContent(ref MemoryBuilder<ReadOnlyMemory<char>> builder)
            {
                Transition.AppendContent(ref builder);
                Name.AppendContent(ref builder);
                Colon?.AppendContent(ref builder);
                ParameterName?.AppendContent(ref builder);
            }
        }
    }
}
