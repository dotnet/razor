// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultCSharpCodeActionProvider : CSharpCodeActionProvider
    {
        private static readonly HashSet<string> SupportedDefaultCodeActionNames = new HashSet<string>()
        {
            // impl interface, abstract class,
            // Generate constructor '.+\(.*\)'
            // "Generate Equals and GetHashCode",
            // "Add 'DebuggerDisplay' attribute"
            //RazorPredefinedCodeRefactoringProviderNames.ImplementInterfaceExplicitly,
            //RazorPredefinedCodeRefactoringProviderNames.ImplementInterfaceImplicitly,
            RazorPredefinedCodeRefactoringProviderNames.GenerateEqualsAndGetHashCodeFromMembers,
            RazorPredefinedCodeRefactoringProviderNames.AddAwait,
            RazorPredefinedCodeRefactoringProviderNames.AddDebuggerDisplay,
            RazorPredefinedCodeFixProviderNames.ImplementAbstractClass,
            RazorPredefinedCodeFixProviderNames.ImplementInterface,
            RazorPredefinedCodeFixProviderNames.GenerateConstructor,
            RazorPredefinedCodeFixProviderNames.SpellCheck,
            RazorPredefinedCodeFixProviderNames.UseIsNullCheck,
            // Create and assign (property|field)
            // "Add null check",
            // "Add null checks for all parameters",

        };

        public override Task<IReadOnlyList<RazorCodeAction>> ProvideAsync(
            RazorCodeActionContext context,
            IEnumerable<RazorCodeAction> codeActions,
            CancellationToken cancellationToken)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (codeActions is null)
            {
                throw new ArgumentNullException(nameof(codeActions));
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


            var results = new List<RazorCodeAction>();

            foreach (var codeAction in codeActions)
            {
                if (SupportedDefaultCodeActionNames.Contains(codeAction.Name))
                {
                    results.Add(codeAction);
                }
            }

            var wrappedResults = results.Select(c => c.WrapResolvableCSharpCodeAction(context)).ToList();
            return Task.FromResult(wrappedResults as IReadOnlyList<RazorCodeAction>);
        }
    }
}
