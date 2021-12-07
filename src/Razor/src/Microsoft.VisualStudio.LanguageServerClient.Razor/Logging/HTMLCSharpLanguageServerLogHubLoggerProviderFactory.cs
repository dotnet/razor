// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Composition;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging
{
    [Shared]
    [Export(typeof(HTMLCSharpLanguageServerLogHubLoggerProviderFactory))]
    internal class HTMLCSharpLanguageServerLogHubLoggerProviderFactory : LogHubLoggerProviderFactoryBase
    {
        [ImportingConstructor]
        public HTMLCSharpLanguageServerLogHubLoggerProviderFactory(RazorLogHubTraceProvider traceProvider) :
            base(traceProvider)
        {
        }
    }
}
