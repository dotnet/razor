// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class VSCompletionListCapability
    {
        /// <summary>
        /// Gets or sets a value indicating whether completion lists can have VSCommitCharacters. These commit characters get propagated
        /// onto underlying valid completion items unless they have their own commit characters.
        /// </summary>
        [JsonProperty("_vs_commitCharacters")]
        public bool CommitCharacters { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether completion lists can have Data bags. These data bags get propagated
        /// onto underlying completion items unless they have their own data bags.
        /// </summary>
        [JsonProperty("_vs_data")]
        public bool Data { get; set; }
    }
}
