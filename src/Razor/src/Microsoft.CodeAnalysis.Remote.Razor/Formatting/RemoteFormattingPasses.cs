// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.CodeAnalysis.Remote.Razor.Formatting;

[Export(typeof(IFormattingPass)), Shared]
[method: ImportingConstructor]
internal sealed class RemoteCSharpFormattingPass(
    IDocumentMappingService documentMappingService,
    ILoggerFactory loggerFactory)
    : CSharpFormattingPass(documentMappingService, loggerFactory);

[Export(typeof(IFormattingPass)), Shared]
[method: ImportingConstructor]
internal sealed class RemoteFormattingContentValidationPass(
    IDocumentMappingService documentMappingService,
    ILoggerFactory loggerFactory)
    : FormattingContentValidationPass(documentMappingService, loggerFactory);

[Export(typeof(IFormattingPass)), Shared]
[method: ImportingConstructor]
internal sealed class RemoteFormattingDiagnosticValidationPass(
    IDocumentMappingService documentMappingService,
    ILoggerFactory loggerFactory)
    : FormattingDiagnosticValidationPass(documentMappingService, loggerFactory);
