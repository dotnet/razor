// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Extensions;
internal static class OmniSharpJsonConverterCollectionExtensions
{
    public static void RegisterOmniSharpRazorConverters(this IList<JsonConverter> collection)
    {
        collection.RegisterRazorConverters();
    }
}
