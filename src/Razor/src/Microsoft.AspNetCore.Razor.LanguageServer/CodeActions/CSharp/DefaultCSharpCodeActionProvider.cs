// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class DefaultCSharpCodeActionProvider : CSharpCodeActionProvider
    {
        private static readonly HashSet<string> SupportedDefaultCodeActionNames = new HashSet<string>()
        {
            // impl interface, abstract class,
            // RazorPredefinedCodeRefactoringProviderNames.ImplementInterfaceExplicitly,
            // Generate constructor '.+\(.*\)'
            // Create and assign (property|field)
            "Generate Equals and GetHashCode",
            "Add null check",
            "Add null checks for all parameters",
            "Add 'DebuggerDisplay' attribute"

        };

        public override Task<IReadOnlyList<CodeAction>> ProvideAsync(
            RazorCodeActionContext context,
            Dictionary<string, List<CodeAction>> codeActionsWithNames,
            CancellationToken cancellationToken)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (codeActionsWithNames is null)
            {
                throw new ArgumentNullException(nameof(codeActionsWithNames));
            }

            // Used to identify if this is VSCode which doesn't support
            // code action resolve.
            if (!context.SupportsCodeActionResolve)
            {
                return EmptyResult;
            }

            // Disable multi-line code actions in @functions block
            // Will be removed once https://github.com/dotnet/aspnetcore/issues/26501 is unblocked.
            if (InFunctionsBlock(context))
            {
                return EmptyResult;
            }


            var results = new List<CodeAction>();

            foreach (var name in SupportedDefaultCodeActionNames)
            {
                if (codeActionsWithNames.TryGetValue(name, out var codeActions))
                {
                    results.AddRange(codeActions);
                }
            }

            var wrappedResults = results.Select(c => c.WrapResolvableCSharpCodeAction(context)).ToList();
            return Task.FromResult(wrappedResults as IReadOnlyList<CodeAction>);
        }
    }
}
