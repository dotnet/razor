// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.VisualStudio.Editor.Razor.Logging;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Logging;

[Shared]
[Export(typeof(OutputWindowLogHubLoggerProviderFactory))]
internal class OutputWindowLogHubLoggerProviderFactory : LogHubLoggerProviderFactoryBase
{
    [ImportingConstructor]
    public OutputWindowLogHubLoggerProviderFactory(RazorLogHubTraceProvider traceProvider)
        : base(traceProvider)
    {
    }
}
