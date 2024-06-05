// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.CodeAnalysis.Remote.Razor.Initialization;

[Shared]
[Export(typeof(ILanguageServerFeatureOptionsProvider))]
internal class RemoteLanguageServerFeatureOptionsProvider(LanguageServerFeatureOptions options) : ILanguageServerFeatureOptionsProvider
{
    public LanguageServerFeatureOptions GetOptions() => options;
}
