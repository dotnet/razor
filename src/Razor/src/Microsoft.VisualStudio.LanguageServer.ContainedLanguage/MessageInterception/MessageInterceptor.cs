// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage.MessageInterception;

/// <summary>
/// Intercepts an LSP message and applies changes to the payload.
/// </summary>
public abstract class MessageInterceptor
{
    /// <summary>
    /// Applies changes to the message token, and signals if the document path has been changed.
    /// </summary>
    /// <param name="message">The message payload</param>
    /// <param name="containedLanguageName">The name of the content type for the contained language.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    [Obsolete("Will be removed in a future version.")]
    public virtual Task<InterceptionResult> ApplyChangesAsync(JToken message, string containedLanguageName, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("This method is obsolete and will be removed in a future version.");
    }

    public virtual Task<InterceptionResult> ApplyChangesAsync<T>(T message, string containedLanguageName, CancellationToken cancellationToken)
    {
        return Task.FromResult(InterceptionResult.NoChange);
    }
}
