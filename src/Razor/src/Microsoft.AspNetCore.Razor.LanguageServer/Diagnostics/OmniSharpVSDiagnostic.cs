// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

#nullable enable

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    internal class OmniSharpVSDiagnostic : Diagnostic
    {
        public static readonly PlatformExtensionConverter<Diagnostic, OmniSharpVSDiagnostic> JsonConverter = new();

        // We need to override the "Tags" property because the basic Diagnostic Tags property has a custom JsonConverter that does not allow
        // VS extensions to tags.
        public new Container<OmniSharpVSDiagnosticTag>? Tags { get; set; }
    }
}
