// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class SemanticTokensHandlerTest
    {
        public SemanticTokensHandlerTest()
        {
            Uri = new Uri("C:/path/to/file.razor");
        }

        private Uri Uri { get; }

        [Fact]
        public Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public Task HandleRequestAsync_FullDocument()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public Task HandleRequestAsync_Delta()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public Task HandleRequestAsync_Range()
        {
            throw new NotImplementedException();
        }
    }
}
