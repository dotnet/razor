﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language;

public interface IImportProjectFeature : IRazorProjectEngineFeature
{
    IReadOnlyList<RazorProjectItem> GetImports(RazorProjectItem projectItem);
}
