﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer;

// We need to keep this in sync with the client definitions like Trace.ts
internal enum Trace
{
    Off = 0,
    Messages = 1,
    Verbose = 2,
}
