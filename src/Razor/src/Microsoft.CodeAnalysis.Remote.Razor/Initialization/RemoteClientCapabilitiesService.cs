// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Razor.Protocol;

namespace Microsoft.CodeAnalysis.Remote.Razor;

[Export(typeof(RemoteClientCapabilitiesService)), Shared]
internal sealed class RemoteClientCapabilitiesService : ClientCapabilitiesServiceBase;
