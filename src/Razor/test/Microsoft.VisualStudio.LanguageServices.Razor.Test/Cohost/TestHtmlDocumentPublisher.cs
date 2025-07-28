// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

internal sealed class TestHtmlDocumentPublisher : IHtmlDocumentPublisher
{
    private readonly List<(TextDocument, string, ChecksumWrapper)> _publishes = [];

    public List<(TextDocument Document, string Text, ChecksumWrapper Checksum)> Publishes => _publishes;

    public Task PublishAsync(TextDocument document, SynchronizationResult synchronizationResult, string htmlText, CancellationToken cancellationToken)
    {
        _publishes.Add((document, htmlText, synchronizationResult.Checksum));
        return Task.CompletedTask;
    }
}
