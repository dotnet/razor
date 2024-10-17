// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor.Settings;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hosting;

[Flags]
internal enum FormattingFlags
{
    Disabled = 0,
    Enabled  = 1,
    OnPaste  = 1 << 1,
    OnType   = 1 << 2,
    All      = Enabled | OnPaste | OnType
};

internal static class FormattingFlagExtensions
{
    public static bool IsEnabled(this FormattingFlags flags)
        => flags.IsFlagSet(FormattingFlags.Enabled);

    public static bool IsOnTypeEnabled(this FormattingFlags flags)
        => flags.IsFlagSet(FormattingFlags.OnType);

    public static bool IsOnPasteEnabled(this FormattingFlags flags)
        => flags.IsFlagSet(FormattingFlags.OnPaste);
}

internal record RazorLSPOptions(
    FormattingFlags Formatting,
    bool AutoClosingTags,
    bool InsertSpaces,
    int TabSize,
    bool AutoShowCompletion,
    bool AutoListParams,
    bool AutoInsertAttributeQuotes,
    bool ColorBackground,
    bool CodeBlockBraceOnNextLine,
    bool CommitElementsWithSpace)
{
    public readonly static RazorLSPOptions Default = new(Formatting: FormattingFlags.All,
                                                         AutoClosingTags: true,
                                                         AutoListParams: true,
                                                         InsertSpaces: true,
                                                         TabSize: 4,
                                                         AutoShowCompletion: true,
                                                         AutoInsertAttributeQuotes: true,
                                                         ColorBackground: false,
                                                         CodeBlockBraceOnNextLine: false,
                                                         CommitElementsWithSpace: true);

    /// <summary>
    /// Initializes the LSP options with the settings from the passed in client settings, and default values for anything
    /// not defined in client settings.
    /// </summary>
    internal static RazorLSPOptions From(ClientSettings settings)
        => new(GetFormattingFlags(settings),
              settings.AdvancedSettings.AutoClosingTags,
              !settings.ClientSpaceSettings.IndentWithTabs,
              settings.ClientSpaceSettings.IndentSize,
              settings.ClientCompletionSettings.AutoShowCompletion,
              settings.ClientCompletionSettings.AutoListParams,
              settings.AdvancedSettings.AutoInsertAttributeQuotes,
              settings.AdvancedSettings.ColorBackground,
              settings.AdvancedSettings.CodeBlockBraceOnNextLine,
              settings.AdvancedSettings.CommitElementsWithSpace);

    private static FormattingFlags GetFormattingFlags(ClientSettings settings)
    {
        var flags = FormattingFlags.Enabled;
        if (settings.AdvancedSettings.FormatOnPaste)
        {
            flags |= FormattingFlags.OnPaste;
        }

        if (settings.AdvancedSettings.FormatOnType)
        {
            flags |= FormattingFlags.OnType;
        }

        return flags;
    }
}
