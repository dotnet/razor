// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

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

            AddConverter(serializer, PlatformAgnosticClientCapabilities.JsonConverter);
            AddConverter(serializer, PlatformAgnosticCompletionCapability.JsonConverter);
            AddConverter(serializer, OmniSharpVSCompletionContext.JsonConverter);
            AddConverter(serializer, OmniSharpVSDiagnostic.JsonConverter);
            AddConverter(serializer, OmniSharpVSCodeActionContext.JsonConverter);

            static void AddConverter(LspSerializer serializer, JsonConverter converter)
            {
                serializer.Settings.Converters.Add(converter);
                serializer.JsonSerializer.Converters.Add(converter);
            }
        }
    }
}
