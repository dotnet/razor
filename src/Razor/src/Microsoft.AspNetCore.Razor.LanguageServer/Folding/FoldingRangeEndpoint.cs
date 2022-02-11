// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Folding
{
    internal class FoldingRangeEndpoint : IFoldingRangeHandler
    {
        private static readonly Container<FoldingRange> EmptyFoldingRange = new Container<FoldingRange>();
        private readonly ClientNotifierServiceBase _languageServer;

        public FoldingRangeEndpoint(ClientNotifierServiceBase languageServer)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            _languageServer = languageServer;
        }

        public FoldingRangeRegistrationOptions GetRegistrationOptions(FoldingRangeCapability capability, ClientCapabilities clientCapabilities)
            => new()
            {
                DocumentSelector = RazorDefaults.Selector,
            };

        public async Task<Container<FoldingRange>?> Handle(FoldingRangeRequestParam request, CancellationToken cancellationToken)
        {
            var delegatedRequest = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorFoldingRangeEndpoint, request).ConfigureAwait(false);
            var foldingRange = await delegatedRequest.Returning<Container<FoldingRange>?>(cancellationToken).ConfigureAwait(false);

            if (foldingRange is null)
            {
                return EmptyFoldingRange;
            }

            // TODO: Map ranges as needed

            return foldingRange;
        }
    }
}
