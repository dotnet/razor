// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal partial class ParserContext
{
    public readonly ref struct ErrorScope
    {
        private readonly ParserContext _context;
        private readonly ErrorSink _oldErrorSink;

        public ErrorScope(ParserContext context)
        {
            _context = context;
            _oldErrorSink = context._errorSink;
        }

        public void Dispose()
        {
            _context._errorSink = _oldErrorSink;
        }
    }
}
