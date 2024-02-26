// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage.MessageInterception;

/// <summary>
/// Contains an updated message token and a signal of whether the document Uri was changed.
/// </summary>
public struct InterceptionResult
{
    public static readonly InterceptionResult NoChange = new((object?)null, false);

    [Obsolete("Will be removed in a future version.")]
    public InterceptionResult(JToken? newToken, bool changedDocumentUri)
        : this((object?)newToken, changedDocumentUri)
    {
    }

    public InterceptionResult(object? newToken, bool changedDocumentUri)
    {
        ChangedToken = newToken;
        ChangedDocumentUri = changedDocumentUri;
    }

    [Obsolete("Will be removed in a future version.")]
    public JToken? UpdatedToken => ChangedToken as JToken;

    public object? ChangedToken { get; }
    public bool ChangedDocumentUri { get; }
}
