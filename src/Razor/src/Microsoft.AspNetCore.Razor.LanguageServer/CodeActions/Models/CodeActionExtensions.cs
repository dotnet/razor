// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal static class CodeActionExtensions
    {
        public static CommandOrCodeAction AsVSCodeCommandOrCodeAction(this RazorCodeAction razorCodeAction)
        {
            if (razorCodeAction.Data is null)
            {
                // Only code action edit, we must convert this to a resolvable command

                var resolutionParams = new RazorCodeActionResolutionParams
                {
                    Action = LanguageServerConstants.CodeActions.EditBasedCodeActionCommand,
                    Language = LanguageServerConstants.CodeActions.Languages.Razor,
                    Data = razorCodeAction.Edit ?? new WorkspaceEdit()
                };

                razorCodeAction = new RazorCodeAction()
                {
                    Title = razorCodeAction.Title,
                    Data = JToken.FromObject(resolutionParams),
                    TelemetryId = razorCodeAction.TelemetryId,
                };
            }

            var serializedParams = JToken.FromObject(razorCodeAction.Data);
            var arguments = new JArray(serializedParams);

            return new CommandOrCodeAction(new Command
            {
                Title = razorCodeAction.Title ?? string.Empty,
                Name = LanguageServerConstants.RazorCodeActionRunnerCommand,
                Arguments = arguments
            });
        }

        public static RazorCodeAction WrapResolvableCSharpCodeAction(
            this RazorCodeAction razorCodeAction,
            RazorCodeActionContext context,
            string action = LanguageServerConstants.CodeActions.Default)
        {
            if (razorCodeAction is null)
            {
                throw new ArgumentNullException(nameof(razorCodeAction));
            }

            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var csharpParams = new CSharpCodeActionParams()
            {
                Data = razorCodeAction.Data,
                RazorFileUri = context.Request.TextDocument.Uri
            };

            var resolutionParams = new RazorCodeActionResolutionParams()
            {
                Action = action,
                Language = LanguageServerConstants.CodeActions.Languages.CSharp,
                Data = csharpParams
            };
            razorCodeAction = razorCodeAction with { Data = JToken.FromObject(resolutionParams) };

            if (razorCodeAction.Children != null)
            {
                for (var i = 0; i < razorCodeAction.Children.Length; i++)
                {
                    razorCodeAction.Children[i] = razorCodeAction.Children[i].WrapResolvableCSharpCodeAction(context, action);
                }
            }

            return razorCodeAction;
        }
    }
}
