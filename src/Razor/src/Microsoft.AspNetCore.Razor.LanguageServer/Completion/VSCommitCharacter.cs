// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class VSCommitCharacter : IEquatable<string>
    {
        [JsonProperty("_vs_character")]
        public string Character { get; set; }

        [JsonProperty("_vs_insert")]
        public bool Insert { get; set; }

        public bool Equals(string other)
        {
            return other == Character;
        }
    }
}
