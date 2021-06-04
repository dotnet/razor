// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultHostWorkspaceServicesProvider : HostWorkspaceServicesProvider
    {
        // We mark this as Lazy because construction of an AdhocWorkspace without services will utilize MEF under the covers
        // which can be expensive and we don't want to do that until absolutely necessary.
        private static readonly Lazy<Workspace> DefaultWorkspace = new Lazy<Workspace>(() => new AdhocWorkspace());

        public override HostWorkspaceServices GetServices() => DefaultWorkspace.Value.Services;
    }
}
