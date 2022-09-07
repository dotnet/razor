// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;

/// <summary>
/// This class is used as a "bridge" between the O# and VS worlds. Ultimately it only exists because the base <see cref="RenameParams"/>
/// type does not implement <see cref="IRequest"/>.
/// </summary>
/// <remarks>
/// Normally we would inherit this from DocumentHighlightParams, which in turn inherits from TextDocumentPositionParams
/// but in this case, we can't do that, we have to go direct.
/// The reason we can't inherit from DocumentHighlightParams is because it uses IProgress, and IProgress is not
/// supported in the middle layer. No, I don't know what "the middle layer" is either. I guess we're in
/// the middle layer right now? No idea. Taylor probably knows, I'm just guessing based on this comment:
/// <![CDATA[
/// https://devdiv.visualstudio.com/DevDiv/_git/VSLanguageServerClient?path=/src/product/RemoteLanguage/Impl/JsonRpcExtensionMethods.cs&version=GC2d1fe0c5ab668d49b0404bed9cd658a78f696165&line=142&lineEnd=143&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents
/// ]]>
/// In case that link goes somewhere bad now, the comment says "IProgress&lt;T&gt; is not supported in MiddleLayer".
///
/// I should mention, its not a complete guess, because we can't ignore the fact that things don't work if
/// we inherit DocumentHighlightParams,  and they do work if we don't.
/// As I always say: "If it hurts when you do that, don't do that".
/// </remarks>
internal class DocumentHighlightParamsBridge : TextDocumentPositionParams, IRequest<DocumentHighlight[]?>, ITextDocumentPositionParams
{
}
