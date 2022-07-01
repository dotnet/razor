// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Test
{
    internal class TestLoggerProvider : HTMLCSharpLanguageServerLogHubLoggerProvider
    {
        public override ILogger CreateLogger(string categoryName) => TestLogger.Instance;
    }
}
