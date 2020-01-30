// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorLSPOptions
    {
        public Trace Trace { get; set; }

        public LogLevel MinLogLevel => GetLogLevelForTrace(Trace);

        public bool EnableFormatting { get; set; }

        public static LogLevel GetLogLevelForTrace(Trace trace)
        {
            return trace switch
            {
                Trace.Off => LogLevel.None,
                Trace.Messages => LogLevel.Information,
                Trace.Verbose => LogLevel.Trace,
                _ => LogLevel.None,
            };
        }
    }
}
