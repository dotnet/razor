// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Completion.Delegation;

/// <summary>
/// Data needed by delegated completion response re-writers
/// </summary>
/// <param name="ProjectedKind">Language kind at the completion position</param>
/// <param name="ProjectedPosition">Cursor position in the language ("projected") document text</param>
/// <remarks>For HTML ProjectedPosition is currently the same position as Razor,
/// for C# it's the corresponding position in the generated C# document</remarks>
internal record struct DelegatedCompletionResponseRewriterParams(RazorLanguageKind ProjectedKind, Position ProjectedPosition);
