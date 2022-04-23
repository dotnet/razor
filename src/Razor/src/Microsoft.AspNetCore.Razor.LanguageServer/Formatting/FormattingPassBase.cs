// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal abstract class FormattingPassBase : IFormattingPass
    {
        protected static readonly int DefaultOrder = 1000;

        public FormattingPassBase(
            RazorDocumentMappingService documentMappingService!!,
            FilePathNormalizer filePathNormalizer!!,
            ClientNotifierServiceBase server!!)
        {
            DocumentMappingService = documentMappingService;
        }

        public abstract bool IsValidationPass { get; }

        public virtual int Order => DefaultOrder;

        protected RazorDocumentMappingService DocumentMappingService { get; }

        public abstract Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken);

        protected TextEdit[] RemapTextEdits(RazorCodeDocument codeDocument!!, TextEdit[] projectedTextEdits!!, RazorLanguageKind projectedKind)
        {
            if (projectedKind != RazorLanguageKind.CSharp)
            {
                // Non C# projections map directly to Razor. No need to remap.
                return projectedTextEdits;
            }

            if (codeDocument.IsUnsupported())
            {
                return Array.Empty<TextEdit>();
            }

            var edits = DocumentMappingService.GetProjectedDocumentEdits(codeDocument, projectedTextEdits);

            return edits;
        }
    }
}
