﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;

namespace Microsoft.AspNetCore.Razor.Language;

public interface IRazorEnginePhase
{
    RazorEngine Engine { get; }

    void Initialize(RazorEngine engine);
    void Execute(RazorCodeDocument codeDocument, CancellationToken cancellationToken = default);
}
