﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class LSPProjectSnapshotManagerDispatcher(ILoggerFactory loggerFactory)
    : ProjectSnapshotManagerDispatcher(loggerFactory)
{
}
