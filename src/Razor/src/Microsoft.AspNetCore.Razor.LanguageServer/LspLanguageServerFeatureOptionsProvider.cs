// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal sealed class LspLanguageServerFeatureOptionsProvider(LanguageServerFeatureOptions options) : ILanguageServerFeatureOptionsProvider
{
    public LanguageServerFeatureOptions GetOptions() => options;
}
