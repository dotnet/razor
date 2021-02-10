// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging
{
    internal class DefaultLogHubLogWriter : LogHubLogWriter, IDisposable
    {
        private TraceSource _traceSource = null;

        public DefaultLogHubLogWriter(TraceSource traceSource)
        {
            if (traceSource is null)
            {
                throw new ArgumentNullException(nameof(traceSource));
            }

            _traceSource = traceSource;
        }

        public override void Write(string message) =>
            _traceSource.TraceInformation(message);

        public override void Write(string format, params object[] args) =>
            _traceSource.TraceInformation(format, args);

        public void Dispose()
        {
            _traceSource?.Flush();
            _traceSource?.Close();
            _traceSource = null;
        }

        public override TraceSource GetTraceSource() => _traceSource;
    }
}
