// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal static class CompletionItemExtensions
    {
        private const string TagHelperElementDataKey = "_TagHelperElementData_";
        private const string TagHelperAttributeDataKey = "_TagHelperAttributes_";
        private const string RazorCompletionItemKind = "_CompletionItemKind_";

        public static bool TryGetRazorCompletionKind(this CompletionItem completion, out RazorCompletionItemKind completionItemKind)
        {
            if (completion is null)
            {
                throw new ArgumentNullException(nameof(completion));
            }

            if (completion.Data is JObject dataObject &&
                dataObject.TryGetValue("data", out var dataToken) &&
                dataToken is JObject data && 
                data.ContainsKey(RazorCompletionItemKind))
            {
                completionItemKind = data[RazorCompletionItemKind].ToObject<RazorCompletionItemKind>();
                return true;
            }

            completionItemKind = default;
            return false;
        }

        public static bool IsTagHelperElementCompletion(this CompletionItem completion)
        {
            if (completion.Data is JObject dataObject &&
                dataObject.TryGetValue("data", out var dataToken) &&
                dataToken is JObject data &&
                data.ContainsKey(TagHelperElementDataKey))
            {
                return true;
            }

            return false;
        }

        public static bool IsTagHelperAttributeCompletion(this CompletionItem completion)
        {
            if (completion.Data is JObject dataObject &&
                dataObject.TryGetValue("data", out var dataToken) &&
                dataToken is JObject data &&
                data.ContainsKey(TagHelperAttributeDataKey))
            {
                return true;
            }

            return false;
        }
    }
}
