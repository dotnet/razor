// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class DefaultHtmlCodeActionProvider : HtmlCodeActionProvider
    {
        private readonly RazorDocumentMappingService _documentMappingService;

        public DefaultHtmlCodeActionProvider(RazorDocumentMappingService documentMappingService)
        {
            _documentMappingService = documentMappingService;
        }

        public override async Task<IReadOnlyList<RazorVSInternalCodeAction>?> ProvideAsync(
            RazorCodeActionContext context,
            IEnumerable<RazorVSInternalCodeAction> codeActions,
            CancellationToken cancellationToken)
        {
            var results = new List<RazorVSInternalCodeAction>();

            foreach (var codeAction in codeActions)
            {
                if (codeAction.Edit is not null)
                {
                    codeAction.Edit = await _documentMappingService.RemapWorkspaceEditAsync(codeAction.Edit, cancellationToken).ConfigureAwait(false);
                }

                results.Add(codeAction);
            }

            return results;
        }
    }
}
