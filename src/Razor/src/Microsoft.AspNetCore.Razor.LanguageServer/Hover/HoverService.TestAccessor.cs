// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Hover;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover;

internal sealed partial class HoverService
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal sealed class TestAccessor(HoverService instance)
    {
        public Task<VSInternalHover?> GetHoverAsync(
            RazorCodeDocument codeDocument,
            string documentFilePath,
            int absoluteIndex,
            HoverDisplayOptions options,
            CancellationToken cancellationToken)
            => HoverFactory.GetHoverAsync(codeDocument, documentFilePath, absoluteIndex, options, instance._projectManager.GetQueryOperations(), cancellationToken);
    }
}
