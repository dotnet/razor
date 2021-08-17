// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Serialization;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Serialization
{
    internal static class LspSerializerExtensions
    {
        public static void RegisterRazorConverters(this LspSerializer serializer)
        {
            if (serializer is null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            // In all of the below we add our converters to both the serializer settings and the actual
            // JsonSerializer. The reasoning behind this choice is that OmniSharp framework is not consistent
            // in using one over the other so we want to protect ourselves.

            serializer.Settings.Converters.RegisterRazorConverters();
            serializer.JsonSerializer.Converters.RegisterRazorConverters();

            serializer.Settings.Converters.Add(PlatformAgnosticClientCapabilities.JsonConverter);
            serializer.JsonSerializer.Converters.Add(PlatformAgnosticClientCapabilities.JsonConverter);
            serializer.Settings.Converters.Add(PlatformAgnosticCompletionCapability.JsonConverter);
            serializer.JsonSerializer.Converters.Add(PlatformAgnosticCompletionCapability.JsonConverter);
            serializer.Settings.Converters.Add(OmniSharpVSCompletionContext.JsonConverter);
            serializer.JsonSerializer.Converters.Add(OmniSharpVSCompletionContext.JsonConverter);
            serializer.Settings.Converters.Add(OmniSharpVSDiagnostic.JsonConverter);
            serializer.JsonSerializer.Converters.Add(OmniSharpVSDiagnostic.JsonConverter);
            serializer.Settings.Converters.Add(OmniSharpVSCodeActionContext.JsonConverter);
            serializer.JsonSerializer.Converters.Add(OmniSharpVSCodeActionContext.JsonConverter);
        }
    }
}
