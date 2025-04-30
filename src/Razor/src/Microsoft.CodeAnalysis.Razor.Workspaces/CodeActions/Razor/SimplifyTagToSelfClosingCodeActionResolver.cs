// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

internal class SimplifyTagToSelfClosingCodeActionResolver() : IRazorCodeActionResolver
{
    public string Action => LanguageServerConstants.CodeActions.SimplifyTagToSelfClosingAction;

    public async Task<WorkspaceEdit?> ResolveAsync(DocumentContext documentContext, JsonElement data, RazorFormattingOptions options, CancellationToken cancellationToken)
    {
        if (data.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var actionParams = JsonSerializer.Deserialize<SimplifyTagToSelfClosingCodeActionParams>(data.GetRawText());
        if (actionParams is null)
        {
            return null;
        }

        var componentDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        if (componentDocument.IsUnsupported())
        {
            return null;
        }

        var text = componentDocument.Source.Text;
        var removeRange = text.GetRange(actionParams.StartTagCloseAngleIndex, actionParams.EndTagCloseAngleIndex);

        var documentChanges = new TextDocumentEdit[]
        {
            new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = documentContext.Uri },
                Edits =
                [
                    new TextEdit
                    {
                        NewText = " />",
                        Range = removeRange,
                    }
                ],
            }
        };

        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges,
        };
    }
}
