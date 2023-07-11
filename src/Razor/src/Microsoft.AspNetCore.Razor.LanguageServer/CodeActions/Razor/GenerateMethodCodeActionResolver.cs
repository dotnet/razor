// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using CSharpSyntaxFactory = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
internal class GenerateMethodCodeActionResolver : IRazorCodeActionResolver
{
    public string Action => LanguageServerConstants.CodeActions.GenerateMethod;
    private readonly DocumentContextFactory _documentContextFactory;
    private readonly RazorLSPOptions _razorLSPOptions;
    private static readonly string s_generatedCode = $$"""0private void $$MethodName$$(){{Environment.NewLine}}0{{{Environment.NewLine}}1throw new System.NotImplementedException();{{Environment.NewLine}}0}""";

    public GenerateMethodCodeActionResolver(DocumentContextFactory documentContextFactory, RazorLSPOptionsMonitor razorLSPOptionsMonitor)
    {
        _documentContextFactory = documentContextFactory;
        _razorLSPOptions = razorLSPOptionsMonitor.CurrentValue;
    }

    public async Task<WorkspaceEdit?> ResolveAsync(JObject data, CancellationToken cancellationToken)
    {
        if (data is null)
        {
            return null;
        }

        var actionParams = data.ToObject<GenerateMethodCodeActionParams>();
        if (actionParams is null)
        {
            return null;
        }

        var documentContext = await _documentContextFactory.TryCreateForOpenDocumentAsync(actionParams.Uri, cancellationToken).ConfigureAwait(false);
        if (documentContext is null)
        {
            return null;
        }

        var code = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var uriPath = FilePathNormalizer.Normalize(actionParams.Uri.GetAbsoluteOrUNCPath());
        var codeBehindPath = $"{uriPath}.cs";
        if (!File.Exists(codeBehindPath))
        {
            return GenerateMethodInCodeBlock(code, actionParams);
        }

        var razorCSharpText = (await documentContext.GetCSharpSourceTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
        var razorNamespace = razorCSharpText[(razorCSharpText.IndexOf("namespace ") + 10)..razorCSharpText.IndexOf("\r\n{")];
        var mock = CSharpSyntaxFactory.ParseCompilationUnit(File.ReadAllText(codeBehindPath));
        var @namespace = mock.Members.Where(m => m is BaseNamespaceDeclarationSyntax { } @namespace && @namespace.Name.ToString() == razorNamespace).FirstOrDefault();
        if (@namespace is null)
        {
            // The code behind file is malformed, generate the code in the razor file instead.
            return GenerateMethodInCodeBlock(code, actionParams);
        }

        var @class = ((BaseNamespaceDeclarationSyntax)@namespace).Members.Where
            (m => m is ClassDeclarationSyntax { } @class && Path.GetFileNameWithoutExtension(uriPath) == @class.Identifier.Text)
            .FirstOrDefault();
        if (@class is null)
        {
            // The code behind file is malformed, generate the code in the razor file instead.
            return GenerateMethodInCodeBlock(code, actionParams);
        }

        var classLocationLineSpan = @class.GetLocation().GetLineSpan();
        var codeBehindUri = new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Path = codeBehindPath,
            Host = string.Empty,
        }.Uri;

        var insertPosition = new Position(classLocationLineSpan.EndLinePosition.Line, 0);
        var newText = $"{FormattingUtilities.AddIndentationToMethod(s_generatedCode, _razorLSPOptions.InsertSpaces, _razorLSPOptions.TabSize, classLocationLineSpan.StartLinePosition.Character)}{Environment.NewLine}";
        var edit = new TextEdit() { Range = new Range { Start = insertPosition, End = insertPosition }, NewText = newText.Replace("$$MethodName$$", actionParams.MethodName) };
        var codeBehindTextDocEdit = new TextDocumentEdit()
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier() { Uri = codeBehindUri },
            Edits = new TextEdit[] { edit }
        };

        return new WorkspaceEdit() { DocumentChanges = new[] { codeBehindTextDocEdit } };
    }

    private WorkspaceEdit GenerateMethodInCodeBlock(RazorCodeDocument code, GenerateMethodCodeActionParams actionParams)
    {
        var edit = CodeBlockService.CreateFormattedTextEdit(code, s_generatedCode, actionParams.MethodName, _razorLSPOptions);
        var razorTextDocEdit = new TextDocumentEdit()
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier() { Uri = actionParams.Uri },
            Edits = new TextEdit[] { edit },
        };

        return new WorkspaceEdit() { DocumentChanges = new[] { razorTextDocEdit } };
    }
}
