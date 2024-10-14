﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Frozen;
using System.Linq;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal static class CompletionTriggerCharacters
{
    public static FrozenSet<string> RazorTriggerCharacters { get; } = new[] { "@", "<", ":", " " }.ToFrozenSet();
    public static FrozenSet<string> RazorDelegationTriggerCharacters { get; } = new[] { "@" }.ToFrozenSet();
    public static FrozenSet<string> CSharpTriggerCharacters { get; } = new[] { " ", "(", "=", "#", ".", "<", "[", "{", "\"", "/", ":", "~" }.ToFrozenSet();
    public static FrozenSet<string> HtmlTriggerCharacters { get; } = new[] { ":", "@", "#", ".", "!", "*", ",", "(", "[", "-", "<", "&", "\\", "/", "'", "\"", "=", ":", " ", "`" }.ToFrozenSet();
    public static FrozenSet<string> AllDelegationTriggerCharacters { get; } = RazorDelegationTriggerCharacters.Union(CSharpTriggerCharacters).Union(HtmlTriggerCharacters).ToFrozenSet();
    public static FrozenSet<string> AllTriggerCharacters { get; } = RazorTriggerCharacters.Union(AllDelegationTriggerCharacters).ToFrozenSet();

    public static bool IsValidTrigger(FrozenSet<string> triggerCharacters, CompletionContext completionContext)
        => completionContext.TriggerKind != CompletionTriggerKind.TriggerCharacter ||
           completionContext.TriggerCharacter is null ||
           triggerCharacters.Contains(completionContext.TriggerCharacter);
}