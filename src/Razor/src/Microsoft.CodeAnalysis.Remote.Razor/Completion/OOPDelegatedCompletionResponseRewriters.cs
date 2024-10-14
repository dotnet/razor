// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.Completion.Delegation;

namespace Microsoft.CodeAnalysis.Remote.Razor.Completion;

[Export(typeof(DelegatedCompletionResponseRewriter)), Shared]
internal class OOPDesignTimeHelperResponseRewriter : DesignTimeHelperResponseRewriter;

[Export(typeof(DelegatedCompletionResponseRewriter)), Shared]
internal class OOPHtmlCommitCharacterResponseRewriter : HtmlCommitCharacterResponseRewriter;

[Export(typeof(DelegatedCompletionResponseRewriter)), Shared]
internal class OOPSnippetResponseRewriter : SnippetResponseRewriter;

[Export(typeof(DelegatedCompletionResponseRewriter)), Shared]
internal class OOPTextEditResponseRewriter : TextEditResponseRewriter;
