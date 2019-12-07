// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using HoverModel = OmniSharp.Extensions.LanguageServer.Protocol.Models.Hover;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    public class ExpandedHoverModel : HoverModel
    {
        public string Name { get; set; }
    }
}
