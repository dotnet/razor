// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal sealed class RefactorComponentCreateParams
    {
        public Uri Uri { get; set; }
        public string Name { get; set; }
        public string Where { get; set;  }
    }
}
