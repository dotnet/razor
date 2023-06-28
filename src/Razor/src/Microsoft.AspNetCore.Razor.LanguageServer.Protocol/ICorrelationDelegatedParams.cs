// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Protocol;

/// <summary>
/// Interface for delegated params that uses correlationId to track telemetry
/// </summary>
internal interface ICorrelationDelegatedParams : IDelegatedParams
{
    public Guid CorrelationId { get; }
}
