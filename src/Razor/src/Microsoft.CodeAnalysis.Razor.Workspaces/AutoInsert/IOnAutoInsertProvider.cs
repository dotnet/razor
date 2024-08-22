﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal interface IOnAutoInsertProvider : IOnAutoInsertTriggerCharacterProvider
{
    public VSInternalDocumentOnAutoInsertResponseItem? TryResolveInsertion(Position position, RazorCodeDocument codeDocument, bool enableAutoClosingTags);
}