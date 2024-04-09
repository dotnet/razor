﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;

internal record RazorResolveCodeActionParams(TextDocumentIdentifier Identifier, int HostDocumentVersion, RazorLanguageKind LanguageKind, CodeAction CodeAction);
