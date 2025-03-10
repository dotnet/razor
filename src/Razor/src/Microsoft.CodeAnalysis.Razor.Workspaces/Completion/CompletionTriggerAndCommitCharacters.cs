// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Frozen;
using System.Linq;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal class CompletionTriggerAndCommitCharacters(LanguageServerFeatureOptions languageServerFeatureOptions)
{
    /// <summary>
    ///  Trigger character that can trigger both Razor and Delegation completion
    /// </summary>
    private const string RazorDelegationTriggerCharacter = "@";

    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;

    private static readonly FrozenSet<string> s_vsHtmlTriggerCharacters = new[] { RazorDelegationTriggerCharacter, ":", "#", ".", "!", "*", ",", "(", "[", "-", "<", "&", "\\", "/", "'", "\"", "=", ":", " ", "`" }.ToFrozenSet();
    private static readonly FrozenSet<string> s_vsCodeHtmlTriggerCharacters = new[] { RazorDelegationTriggerCharacter, "#", ".", "!", ",", "-", "<", }.ToFrozenSet();
    private FrozenSet<string>? _allDelegationTriggerCharacters;
    private string[]? _allTriggerCharacters;

    private static readonly FrozenSet<string> s_razorTriggerCharacters = new[] { RazorDelegationTriggerCharacter, "<", ":", " " }.ToFrozenSet();
    private static readonly FrozenSet<string> s_razorDelegationTriggerCharacters = new[] { RazorDelegationTriggerCharacter }.ToFrozenSet();
    private static readonly FrozenSet<string> s_csharpTriggerCharacters = new[] { " ", "(", "=", "#", ".", "<", "[", "{", "\"", "/", ":", "~" }.ToFrozenSet();

    private FrozenSet<string> HtmlTriggerCharacters =>
        _languageServerFeatureOptions.UseVsCodeCompletionTriggerCharacters ? s_vsCodeHtmlTriggerCharacters : s_vsHtmlTriggerCharacters;

    private FrozenSet<string> AllDelegationTriggerCharacters => _allDelegationTriggerCharacters
        ??= s_razorDelegationTriggerCharacters.Union(s_csharpTriggerCharacters).Union(HtmlTriggerCharacters).ToFrozenSet();

    public string[] AllTriggerCharacters => _allTriggerCharacters ??= [.. s_razorTriggerCharacters.Union(AllDelegationTriggerCharacters)];

    /// <summary>
    /// This is the intersection of C# and HTML commit characters.
    /// </summary>
    // We need to specify it so that platform can correctly calculate ApplicableToSpan in
    // https://devdiv.visualstudio.com/DevDiv/_git/VSLanguageServerClient?path=/src/product/RemoteLanguage/Impl/Features/Completion/AsyncCompletionSource.cs&version=GBdevelop&line=855&lineEnd=855&lineStartColumn=9&lineEndColumn=49&lineStyle=plain&_a=contents
    // This is needed to fix https://github.com/dotnet/razor/issues/10787 in particular
    public static string[] AllCommitCharacters = [" ", ">", ";", "="];

    public static bool IsValidTrigger(FrozenSet<string> triggerCharacters, CompletionContext completionContext)
        => completionContext.TriggerKind != CompletionTriggerKind.TriggerCharacter ||
           completionContext.TriggerCharacter is null ||
           triggerCharacters.Contains(completionContext.TriggerCharacter);

    public bool IsValidCSharpTrigger(CompletionContext completionContext)
        => IsValidTrigger(s_csharpTriggerCharacters, completionContext);

    public bool IsValidDelegationTrigger(CompletionContext completionContext)
        => IsValidTrigger(AllDelegationTriggerCharacters, completionContext);

    public bool IsValidHtmlTrigger(CompletionContext completionContext)
        => IsValidTrigger(HtmlTriggerCharacters, completionContext);

    public bool IsValidRazorTrigger(CompletionContext completionContext)
        => IsValidTrigger(s_razorTriggerCharacters, completionContext);

    public bool IsCSharpTriggerCharacter(string ch)
        => s_csharpTriggerCharacters.Contains(ch);

    public bool IsDelegationTriggerCharacter(string ch)
        => AllDelegationTriggerCharacters.Contains(ch);

    public bool IsHtmlTriggerCharacter(string ch)
        => HtmlTriggerCharacters.Contains(ch);

    public bool IsRazorTriggerCharacter(string ch)
        => s_razorTriggerCharacters.Contains(ch);

    public bool IsRazorDelegationTriggerCharacter(string ch)
        => ch == RazorDelegationTriggerCharacter;
}
