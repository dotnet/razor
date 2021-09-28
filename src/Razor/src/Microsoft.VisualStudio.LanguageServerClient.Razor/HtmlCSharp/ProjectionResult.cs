﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal class ProjectionResult
    {
        public Uri Uri { get; set; }

        public Position Position { get; set; }

        public int PositionIndex { get; set; }

        public RazorLanguageKind LanguageKind { get; set; }

        public int? HostDocumentVersion { get; set; }
    }
}
