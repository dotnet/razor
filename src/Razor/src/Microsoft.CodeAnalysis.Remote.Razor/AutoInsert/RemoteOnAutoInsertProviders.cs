// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Text;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.CodeAnalysis.Remote.Razor.AutoInsert;

[Shared]
[Export(typeof(IOnAutoInsertProvider))]
internal sealed class RemoteAutoClosingTagOnAutoInsertProvider(ILoggerFactory loggerFactory)
    : AutoClosingTagOnAutoInsertProvider(loggerFactory);

[Shared]
[Export(typeof(IOnAutoInsertProvider))]
internal sealed class RemoteCloseTextTagOnAutoInsertProvider(ILoggerFactory loggerFactory)
    : CloseTextTagOnAutoInsertProvider(loggerFactory);
