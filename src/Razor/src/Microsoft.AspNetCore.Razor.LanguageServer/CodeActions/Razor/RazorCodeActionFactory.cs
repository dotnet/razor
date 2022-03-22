// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

internal static class RazorCodeActionFactory
{
    private readonly static Guid s_addComponentUsingTelemetryId = new("6c5416b7-7be7-49ee-aa60-904385be676f");
    private readonly static Guid s_fullyQualifyComponentTelemetryId = new("3d9abe36-7d10-4e08-8c18-ad88baa9a923");
    private readonly static Guid s_createComponentFromTagTelemetryId = new("a28e0baa-a4d5-4953-a817-1db586035841");
    private readonly static Guid s_createExtractToCodeBehindTelemetryId = new("f63167f7-fdc6-450f-8b7b-b240892f4a27");

    public static RazorCodeAction CreateAddComponentUsing(string @namespace, RazorCodeActionResolutionParams resolutionParams)
    {
        var title = $"@using {@namespace}";
        var data = JToken.FromObject(resolutionParams);
        var codeAction = new RazorCodeAction
        {
            Title = title,
            Data = data,
            TelemetryId = s_addComponentUsingTelemetryId,
        };
        return codeAction;
    }

    public static RazorCodeAction CreateFullyQualifyComponent(string fullyQualifiedName, WorkspaceEdit workspaceEdit)
    {
        var codeAction = new RazorCodeAction()
        {
            Title = fullyQualifiedName,
            Edit = workspaceEdit,
            TelemetryId = s_fullyQualifyComponentTelemetryId,
        };
        return codeAction;
    }

    public static RazorCodeAction CreateComponentFromTag(RazorCodeActionResolutionParams resolutionParams)
    {
        var title = RazorLS.Resources.Create_Component_FromTag_Title;
        var data = JToken.FromObject(resolutionParams);
        var codeAction = new RazorCodeAction()
        {
            Title = title,
            Data = data,
            TelemetryId = s_createComponentFromTagTelemetryId,
        };
        return codeAction;
    }

    public static RazorCodeAction CreateExtractToCodeBehind(RazorCodeActionResolutionParams resolutionParams)
    {
        var title = RazorLS.Resources.ExtractTo_CodeBehind_Title;
        var data = JToken.FromObject(resolutionParams);
        var codeAction = new RazorCodeAction()
        {
            Title = title,
            Data = data,
            TelemetryId = s_createExtractToCodeBehindTelemetryId,
        };
        return codeAction;
    }
}
