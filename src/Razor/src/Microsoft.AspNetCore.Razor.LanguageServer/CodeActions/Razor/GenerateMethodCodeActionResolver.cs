// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
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
    private readonly DocumentContextFactory _documentContextFactory;
    private readonly RazorLSPOptionsMonitor _razorLSPOptionsMonitor;
    private readonly ClientNotifierServiceBase _languageServer;

    private static readonly string s_beginningIndents = $"{FormattingUtilities.InitialIndent}{FormattingUtilities.Indent}";
    private static readonly string s_returnType = "$$ReturnType$$";
    private static readonly string s_methodName = "$$MethodName$$";
    private static readonly string s_eventArgs = "$$EventArgs$$";
    private static readonly string s_methodContent = "$$MethodContent$$";
    private static readonly string s_generateMethodTemplate =
        $"{s_beginningIndents}private {s_returnType} {s_methodName}({s_eventArgs}){Environment.NewLine}" +
        s_beginningIndents + "{" + Environment.NewLine +
        $"{s_beginningIndents}{FormattingUtilities.Indent}throw new {s_methodContent}();{Environment.NewLine}" +
        s_beginningIndents + "}";

    public string Action => LanguageServerConstants.CodeActions.GenerateEventHandler;

    public GenerateMethodCodeActionResolver(DocumentContextFactory documentContextFactory, RazorLSPOptionsMonitor razorLSPOptionsMonitor, ClientNotifierServiceBase languageServer)
    {
        _documentContextFactory = documentContextFactory;
        _razorLSPOptionsMonitor = razorLSPOptionsMonitor;
        _languageServer = languageServer;
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

        var documentContext = _documentContextFactory.TryCreateForOpenDocument(actionParams.Uri);
        if (documentContext is null)
        {
            return null;
        }

        var code = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var uriPath = FilePathNormalizer.Normalize(actionParams.Uri.GetAbsoluteOrUNCPath());
        var razorClassName = Path.GetFileNameWithoutExtension(uriPath);
        var codeBehindPath = $"{uriPath}.cs";

        if (!File.Exists(codeBehindPath)
            || razorClassName is null
            || !code.TryComputeNamespace(fallbackToRootNamespace: true, out var razorNamespace))
        {
            return await GenerateMethodInCodeBlockAsync(code, actionParams, documentContext, cancellationToken).ConfigureAwait(false);
        }

        var content = File.ReadAllText(codeBehindPath);
        var mock = CSharpSyntaxFactory.ParseCompilationUnit(content);
        var @namespace = mock.Members
            .FirstOrDefault(m => m is BaseNamespaceDeclarationSyntax { } @namespace && @namespace.Name.ToString() == razorNamespace);
        if (@namespace is null)
        {
            // The code behind file is malformed, generate the code in the razor file instead.
            return await GenerateMethodInCodeBlockAsync(code, actionParams, documentContext, cancellationToken).ConfigureAwait(false);
        }

        var @class = ((BaseNamespaceDeclarationSyntax)@namespace).Members
            .FirstOrDefault(m => m is ClassDeclarationSyntax { } @class && razorClassName == @class.Identifier.Text);
        if (@class is null)
        {
            // The code behind file is malformed, generate the code in the razor file instead.
            return await GenerateMethodInCodeBlockAsync(code, actionParams, documentContext, cancellationToken).ConfigureAwait(false);
        }

        var codeBehindUri = new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Path = codeBehindPath,
            Host = string.Empty,
        }.Uri;

        var codeBehindTextDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier() { Uri = codeBehindUri };

        var templateWithMethodSignature = await PopulateMethodSignatureAsync(
            documentContext,
            actionParams,
            content,
            codeBehindTextDocumentIdentifier,
            cancellationToken).ConfigureAwait(false);

        var classLocationLineSpan = @class.GetLocation().GetLineSpan();
        var formattedMethod = FormattingUtilities.AddIndentationToMethod(
            templateWithMethodSignature,
            _razorLSPOptionsMonitor.CurrentValue,
            @class.SpanStart,
            classLocationLineSpan.StartLinePosition.Character,
            content);

        var insertPosition = new Position(classLocationLineSpan.EndLinePosition.Line, 0);
        var edit = new TextEdit()
        {
            Range = new Range { Start = insertPosition, End = insertPosition },
            NewText = $"{formattedMethod}{Environment.NewLine}"
        };

        var codeBehindTextDocEdit = new TextDocumentEdit()
        {
            TextDocument = codeBehindTextDocumentIdentifier,
            Edits = new TextEdit[] { edit }
        };

        return new WorkspaceEdit() { DocumentChanges = new[] { codeBehindTextDocEdit } };
    }

    private async Task<WorkspaceEdit> GenerateMethodInCodeBlockAsync(
        RazorCodeDocument code,
        GenerateMethodCodeActionParams actionParams,
        VersionedDocumentContext documentContext,
        CancellationToken cancellationToken)
    {
        var csharpSource = await documentContext.GetCSharpSourceTextAsync(cancellationToken).ConfigureAwait(false);
        var templateWithMethodSignature = await PopulateMethodSignatureAsync(
            documentContext,
            actionParams,
            csharpSource.ToString(),
            codeBehindIdentifier: null,
            cancellationToken).ConfigureAwait(false);

        var edit = CodeBlockService.CreateFormattedTextEdit(code, templateWithMethodSignature, _razorLSPOptionsMonitor.CurrentValue);
        var razorTextDocEdit = new TextDocumentEdit()
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier() { Uri = actionParams.Uri },
            Edits = new TextEdit[] { edit },
        };

        return new WorkspaceEdit() { DocumentChanges = new[] { razorTextDocEdit } };
    }

    private async Task<string> PopulateMethodSignatureAsync(
        VersionedDocumentContext documentContext,
        GenerateMethodCodeActionParams actionParams,
        string csharpSource,
        OptionalVersionedTextDocumentIdentifier? codeBehindIdentifier,
        CancellationToken cancellationToken)
    {
        var templateWithMethodSignature = s_generateMethodTemplate.Replace(s_methodName, actionParams.MethodName);

        var returnType = actionParams.IsAsync ? "System.Threading.Tasks.Task" : "void";

        var eventTagHelper = documentContext.Project.TagHelpers
            .FirstOrDefault(th => th.Name == actionParams.EventName && th.IsEventHandlerTagHelper() && th.GetEventArgsType() is not null);
        var eventArgsType = eventTagHelper is null
            ? string.Empty // Couldn't find the params, generate no params instead.
            : eventTagHelper.GetEventArgsType();

        var fullyQualifiedTypeNames = new[] { "System.NotImplementedException", returnType, eventArgsType };
        var absoluteIndex = csharpSource.IndexOf($"class {Path.GetFileNameWithoutExtension(documentContext.FilePath)}");

        var delegatedParams = new DelegatedSimplifyTypeNamesParams(
            documentContext.Identifier,
            codeBehindIdentifier,
            fullyQualifiedTypeNames,
            absoluteIndex);

        var result = await _languageServer.SendRequestAsync<DelegatedSimplifyTypeNamesParams, string[]?>(
            CustomMessageNames.RazorSimplifyTypeEndpointName,
            delegatedParams,
            cancellationToken).ConfigureAwait(false)
            ?? fullyQualifiedTypeNames;

        return templateWithMethodSignature
            .Replace(s_methodContent, result[0])
            .Replace(s_returnType, result[1])
            .Replace(s_eventArgs, $"{result[2]} e");
    }
}
