﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

[Export(typeof(IDocumentContextFactory)), Shared]
internal class DocumentContextFactory : IDocumentContextFactory
{
    public bool TryCreate(
        Uri documentUri,
        VSProjectContext? projectContext,
        bool versioned,
        [NotNullWhen(true)] out DocumentContext? context)
    {
        throw new NotSupportedException("OOP doesn't support this yet, because we don't have a way to pass in the right solution snapshot to use");
    }
}
