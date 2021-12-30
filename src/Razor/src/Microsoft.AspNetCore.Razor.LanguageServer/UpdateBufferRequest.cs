﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class UpdateBufferRequest
    {
        public int? HostDocumentVersion { get; set; }

        public string HostDocumentFilePath { get; set; }

        public IReadOnlyList<TextChange> Changes { get; set; }
    }
}
