// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Telemetry;

namespace Microsoft.CodeAnalysis.Remote.Razor.DocumentMapping;

[Export(typeof(IRazorEditService)), Shared]
[method: ImportingConstructor]
internal sealed class RemoteRazorEditService(IDocumentMappingService documentMappingService, ITelemetryReporter telemetryReporter)
    : RazorEditService(documentMappingService, telemetryReporter);
