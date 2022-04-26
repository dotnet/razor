// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    /// <summary>
    /// Equivalent to VS' ImageElement. The class has been adapted here so we can
    /// use it for LSP serialization since we don't have access to the VS version.
    /// Refer to original class for additional details.
    /// </summary>
    internal class VSImageElement
    {
        [JsonProperty("_vs_type")]
        public static readonly string Type = "ImageElement";

        public static readonly VSImageElement Empty = new(default, string.Empty);

        public VSImageElement(VSImageId imageId)
        {
            ImageId = imageId;
        }

        public VSImageElement(VSImageId imageId, string automationName)
            : this(imageId)
        {
            AutomationName = automationName ?? throw new ArgumentNullException(nameof(automationName));
        }

        [JsonProperty("ImageId")]
        public VSImageId ImageId { get; }

        [JsonProperty("AutomationName")]
        public string AutomationName { get; }
    }
}
