// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.Editor.Razor.Logging;
internal class RazorOutputWindowLoggerProvider : ILoggerProvider
{
    private readonly IOutputWindowLogger _logger;

    public RazorOutputWindowLoggerProvider(IOutputWindowLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ILogger CreateLogger(string _)
    {
        return _logger;
    }

    public void Dispose()
    {
    }
}
