// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Completion.Test
{
    public class RazorCompletionContextTest
    {
        [Fact]
        public void Constructor_ThrowsIfSyntaxTreeNull()
        {
            Assert.Throws<ArgumentNullException>(() => new RazorCompletionContext(tagHelperDocumentContext: null!, syntaxTree: null!));
        }
    }
}
#nullable disable
