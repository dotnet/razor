// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language;

internal class DefaultRazorHtmlDocument : RazorHtmlDocument
{
    private readonly string _generatedHtml;
    private readonly RazorCodeGenerationOptions _options;
    private readonly SourceMapping[] _sourceMappings;

    public DefaultRazorHtmlDocument(
        string generatedHtml,
        RazorCodeGenerationOptions options,
        SourceMapping[] sourceMappings)
    {
        if (generatedHtml == null)
        {
            throw new ArgumentNullException(nameof(generatedHtml));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _generatedHtml = generatedHtml;
        _options = options;
        _sourceMappings = sourceMappings;
    }

    public override string GeneratedCode => _generatedHtml;

    public override RazorCodeGenerationOptions Options => _options;

    public override IReadOnlyList<SourceMapping> SourceMappings => _sourceMappings;
}
