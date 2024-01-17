// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.ProjectEngineHost;

namespace Microsoft.CodeAnalysis.Razor;

// Used to create the 'fallback' project engine when we don't have a custom implementation.
internal interface IFallbackProjectEngineFactory : IProjectEngineFactory
{
}
