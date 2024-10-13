// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.Completion;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Completion;

/// <summary>
/// Helper methods for C# and HTML completion ("delegated" completion) that are used both in LSP and cohosting
/// completion handler code.
/// </summary>
internal static class DelegatedCompletionHelper
{
    /// <summary>
    /// Modifies completion context if needed so that it's acceptable to the delegated language.
    /// </summary>
    /// <param name="context">Original completion context passed to the completion handler</param>
    /// <param name="languageKind">Language of the completion position</param>
    /// <returns>Possibly modified completion context</returns>
    /// <remarks>For example, if we invoke C# completion in Razor via @ character, we will not
    /// want C# to see @ as the trigger character and instead will transform completion context
    /// into "invoked" and "explicit" rather than "typing", without a trigger character</remarks>
    public static VSInternalCompletionContext RewriteContext(VSInternalCompletionContext context, RazorLanguageKind languageKind)
    {
        Debug.Assert(languageKind != RazorLanguageKind.Razor,
            $"{nameof(RewriteContext)} should be called for delegated completion only");

        if (context.TriggerKind != CompletionTriggerKind.TriggerCharacter ||
            context.TriggerCharacter is not { } triggerCharacter)
        {
            // Non-triggered based completion, the existing context is valid.
            return context;
        }

        if (languageKind == RazorLanguageKind.CSharp
            && CompletionTriggerCharacters.CSharpTriggerCharacters.Contains(triggerCharacter))
        {
            // C# trigger character for C# content
            return context;
        }

        if (languageKind == RazorLanguageKind.Html
            && CompletionTriggerCharacters.HtmlTriggerCharacters.Contains(triggerCharacter))
        {
            // HTML trigger character for HTML content
            return context;
        }

        // Trigger character not associated with the current language. Transform the context into an invoked context.
        var rewrittenContext = new VSInternalCompletionContext()
        {
            InvokeKind = context.InvokeKind,
            TriggerKind = CompletionTriggerKind.Invoked,
        };

        if (languageKind == RazorLanguageKind.CSharp
            && CompletionTriggerCharacters.RazorDelegationTriggerCharacters.Contains(triggerCharacter))
        {
            // The C# language server will not return any completions for the '@' character unless we
            // send the completion request explicitly.
            rewrittenContext.InvokeKind = VSInternalCompletionInvokeKind.Explicit;
        }

        return rewrittenContext;
    }

    /// <summary>
    /// Returns possibly update document position info and provisional edit (if any)
    /// </summary>
    /// <param name="documentContext"></param>
    /// <param name="completionContext"></param>
    /// <param name="positionInfo">Original position info</param>
    /// <param name="documentMappingService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <remarks>
    /// Provisional completion happens when typing something like @DateTime. in a document.
    /// In this case the '.' initially is parsed as belonging to HTML. However, we want to
    /// show C# member completion in this case, so we want to make a temporary change to the
    /// generated C# code so that '.' ends up in C#. This method will check for such case,
    /// and provisional completion case is detected, will update position language from HTML
    /// to C# and will return a temporary edit that should be made to the generated document
    /// in order to add the '.' to the generated C# contents.
    /// </remarks>
    public static async Task<CompletionPositionInfo?> TryGetProvisionalCompletionInfoAsync(
        DocumentContext documentContext,
        VSInternalCompletionContext completionContext,
        DocumentPositionInfo positionInfo,
        IDocumentMappingService documentMappingService,
        CancellationToken cancellationToken)
    {
        if (positionInfo.LanguageKind != RazorLanguageKind.Html ||
            completionContext.TriggerKind != CompletionTriggerKind.TriggerCharacter ||
            completionContext.TriggerCharacter != ".")
        {
            // Invalid provisional completion context
            return null;
        }

        if (positionInfo.Position.Character == 0)
        {
            // We're at the start of line. Can't have provisional completions here.
            return null;
        }

        var previousCharacterPositionInfo = await documentMappingService
            .GetPositionInfoAsync(documentContext, positionInfo.HostDocumentIndex - 1, cancellationToken)
            .ConfigureAwait(false);

        if (previousCharacterPositionInfo.LanguageKind != RazorLanguageKind.CSharp)
        {
            return null;
        }

        var previousPosition = previousCharacterPositionInfo.Position;

        // Edit the CSharp projected document to contain a '.'. This allows C# completion to provide valid
        // completion items for moments when a user has typed a '.' that's typically interpreted as Html.
        var addProvisionalDot = VsLspFactory.CreateTextEdit(previousPosition, ".");

        var provisionalPositionInfo = new DocumentPositionInfo
        {
            LanguageKind = RazorLanguageKind.CSharp,
            Position = VsLspFactory.CreatePosition(
                previousPosition.Line,
                previousPosition.Character + 1),
            HostDocumentIndex = previousCharacterPositionInfo.HostDocumentIndex + 1
        };

        return new CompletionPositionInfo()
        {
            ProvisionalTextEdit = addProvisionalDot,
            DocumentPositionInfo = provisionalPositionInfo
        };
    }
}
