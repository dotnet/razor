// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    /// <summary>
    /// Equivalent to VS' ClassifiedTextElement. The class has been adapted here so we
    /// can use it for LSP serialization since we don't have access to the VS version.
    /// Refer to original class for additional details.
    /// </summary>
    internal sealed class RazorClassifiedTextElement
    {
        public const string TextClassificationTypeName = "text";

        public RazorClassifiedTextElement(params RazorClassifiedTextRun[] runs)
        {
            Runs = runs?.ToImmutableList() ?? throw new ArgumentNullException(nameof(runs));
        }

        public RazorClassifiedTextElement(IEnumerable<RazorClassifiedTextRun> runs)
        {
            Runs = runs?.ToImmutableList() ?? throw new ArgumentNullException(nameof(runs));
        }

        public IEnumerable<RazorClassifiedTextRun> Runs { get; }

        public static RazorClassifiedTextElement CreateHyperlink(string text, string tooltip, Action navigationAction)
        {
            Requires.NotNull(text, nameof(text));
            Requires.NotNull(navigationAction, nameof(navigationAction));
            return new RazorClassifiedTextElement(new RazorClassifiedTextRun(TextClassificationTypeName, text, navigationAction, tooltip));
        }

        public static RazorClassifiedTextElement CreatePlainText(string text)
        {
            Requires.NotNull(text, nameof(text));
            return new RazorClassifiedTextElement(new RazorClassifiedTextRun(TextClassificationTypeName, text, RazorClassifiedTextRunStyle.Plain));
        }
    }
}
