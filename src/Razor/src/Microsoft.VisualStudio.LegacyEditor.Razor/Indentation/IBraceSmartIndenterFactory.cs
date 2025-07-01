﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.VisualStudio.LegacyEditor.Razor.Indentation;

internal interface IBraceSmartIndenterFactory
{
    BraceSmartIndenter Create(IVisualStudioDocumentTracker documentTracker);
}
