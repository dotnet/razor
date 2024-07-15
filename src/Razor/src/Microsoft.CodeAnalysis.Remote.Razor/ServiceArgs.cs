// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Composition;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal readonly record struct ServiceArgs(
    IRazorServiceBroker ServiceBroker,
    ExportProvider ExportProvider);
