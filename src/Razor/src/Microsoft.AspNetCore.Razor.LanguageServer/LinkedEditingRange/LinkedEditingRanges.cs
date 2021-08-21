// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.LinkedEditingRange
{
    internal class LinkedEditingRanges
    {
        [JsonProperty("ranges")]
        public Range[] Ranges
        {
            get;
            set;
        }

        //
        // Summary:
        //     Gets or sets the word pattern for the type rename.
        [JsonProperty("wordPattern")]
        public string WordPattern
        {
            get;
            set;
        }
    }
}
