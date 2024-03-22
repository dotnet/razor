// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor.Settings;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Logging;

[Export(typeof(IRazorLoggerProvider))]
internal partial class VisualStudioFeedbackLoggerProvider : IRazorLoggerProvider
{
    private const int BufferSize = 5000;
    private InMemoryBuffer _buffer = new(BufferSize);
    private readonly IClientSettingsManager _clientSettingsManager;

    [ImportingConstructor]
    public VisualStudioFeedbackLoggerProvider(IClientSettingsManager clientSettingsManager)
    {
        _clientSettingsManager = clientSettingsManager;
        _clientSettingsManager.FeedbackRecordingChanged += OnFeedbackRecordingChanged;
    }

    private void OnFeedbackRecordingChanged(object sender, bool isFeedbackRecording)
    {
        if (!isFeedbackRecording)
        {
            Interlocked.Exchange(ref _buffer, new(BufferSize));
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new InMemoryLogger(_buffer, categoryName, _clientSettingsManager);
    }

    public void Dispose()
    {
    }
}
