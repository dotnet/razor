﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

internal abstract class LogHubLogWriter
{
    public abstract TraceSource GetTraceSource();

    public abstract void TraceInformation(string format, params object[] args);

    public abstract void TraceWarning(string format, params object[] args);

    public abstract void TraceError(string format, params object[] args);
}
