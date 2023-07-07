// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
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
    private static readonly string s_generatedCode = "0private void 2()\r\n0{\r\n1throw new NotImplementedException();\r\n0}";

    public GenerateMethodCodeActionResolver(DocumentContextFactory documentContextFactory)
    {
        _documentContextFactory = documentContextFactory;
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
        var @namespace = mock.Members.Where(m => m is NamespaceDeclarationSyntax { } @namespace && @namespace.Name.ToString() == razorNamespace).FirstOrDefault();
        if (@namespace is null)
        {
            // The code behind file is malformed, generate the code in the razor file instead.
            return GenerateMethodInCodeBlock(code, actionParams);
        }

        var @class = ((NamespaceDeclarationSyntax)@namespace).Members.Where
            (m => m is ClassDeclarationSyntax { } @class && uriPath.LastIndexOf(@class.Identifier.Text) != -1 && uriPath[uriPath.LastIndexOf(@class.Identifier.Text)..uriPath.LastIndexOf('.')] == @class.Identifier.Text)
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
        var newText = $"{GenerateMethodIndentService.AddIndentation(s_generatedCode, code.GetCodeGenerationOptions().IndentSize, classLocationLineSpan.StartLinePosition.Character)}\r\n";
        var edit = new TextEdit() { Range = new Range { Start = insertPosition, End = insertPosition }, NewText = newText.Replace("2", actionParams.MethodName) };
        var codeBehindTextDocEdit = new TextDocumentEdit()
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier() { Uri = codeBehindUri },
            Edits = new TextEdit[] { edit }
        };

        return new WorkspaceEdit() { DocumentChanges = new[] { codeBehindTextDocEdit } };
    }

    private static WorkspaceEdit GenerateMethodInCodeBlock(RazorCodeDocument code, GenerateMethodCodeActionParams actionParams)
    {
        var edit = CodeBlockService.CreateFormattedTextEdit(code, s_generatedCode, actionParams.MethodName);
        var razorTextDocEdit = new TextDocumentEdit()
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier() { Uri = actionParams.Uri },
            Edits = new TextEdit[] { edit },
        };

        return new WorkspaceEdit() { DocumentChanges = new[] { razorTextDocEdit } };
    }
}
