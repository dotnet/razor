// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor.Logging;
using Microsoft.VisualStudio.Editor.Razor.Settings;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

[Export(typeof(IRazorLoggerProvider))]
internal sealed class RazorLogHubLoggerProvider : IRazorLoggerProvider
{
    private readonly IRazorLogHubTraceProvider _traceProvider;
    private readonly IClientSettingsManager _clientSettingsManager;

    [ImportingConstructor]
    public RazorLogHubLoggerProvider(IRazorLogHubTraceProvider traceProvider, IClientSettingsManager clientSettingsManager)
    {
        _traceProvider = traceProvider;
        _clientSettingsManager = clientSettingsManager;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new RazorLogHubLogger(categoryName, _traceProvider, _clientSettingsManager);
    }

    public void Dispose()
    {
    }
}
