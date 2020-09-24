// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal class AssortedCodeActionProvider : CSharpCodeActionProvider
    {
        // Supports generating the empty constructor `ClassName()`, as well as constructor with args `ClassName(int)`
        private static readonly string GenerateConstructorCodeActionTitlePattern = @"Generate constructor '.+\(.*\)'";
        private static readonly Regex GenerateConstructorCodeActionRegex = new Regex(GenerateConstructorCodeActionTitlePattern, RegexOptions.IgnoreCase);

        private static readonly string GenerateEqualsAndGetHashCodeCodeActionTitle = "Generate Equals and GetHashCode";
        private static readonly string AddDebuggerDisplayAttributeCodeActionTitle = "Add 'DebuggerDisplay' attribute";

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

            var results = codeActions.Where(c =>
                IsGenerateConstructorCodeAction(c) ||
                IsGenerateEqualsAndGetHashCodeCodeAction(c) ||

                // Temporarily disable till we can support multi-part edit formatting
                // IsAddDebuggerDisplayAttributeCodeAction(c)
            );

            var wrappedResults = results.Select(c => c.WrapCSharpCodeAction(context)).ToList();

            return Task.FromResult(wrappedResults as IReadOnlyList<RazorCodeAction>);
        }

        internal bool IsGenerateConstructorCodeAction(RazorCodeAction codeAction)
            => GenerateConstructorCodeActionRegex.Match(codeAction.Title).Success;

        internal bool IsGenerateEqualsAndGetHashCodeCodeAction(RazorCodeAction codeAction)
            => codeAction.Title.Equals(GenerateEqualsAndGetHashCodeCodeActionTitle, StringComparison.Ordinal);

        internal bool IsAddDebuggerDisplayAttributeCodeAction(RazorCodeAction codeAction)
            => codeAction.Title.Equals(AddDebuggerDisplayAttributeCodeActionTitle, StringComparison.Ordinal);
    }
}
