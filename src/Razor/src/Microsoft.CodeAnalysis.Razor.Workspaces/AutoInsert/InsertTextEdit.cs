// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.AutoInsert;

internal record struct InsertTextEdit(TextEdit TextEdit, InsertTextFormat InsertTextFormat);
