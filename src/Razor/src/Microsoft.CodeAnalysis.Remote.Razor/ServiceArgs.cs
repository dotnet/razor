// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Composition;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal readonly struct ServiceArgs(IRazorServiceBroker serviceBroker, ExportProvider exportProvider)
{
    public readonly IRazorServiceBroker ServiceBroker = serviceBroker;
    public readonly ExportProvider ExportProvider = exportProvider;
}
