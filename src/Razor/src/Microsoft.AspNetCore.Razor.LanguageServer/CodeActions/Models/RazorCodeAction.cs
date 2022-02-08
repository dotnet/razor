// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;

[DebuggerDisplay("{Title,nq}")]
internal record RazorCodeAction : CodeAction, IRequest<RazorCodeAction>, IBaseRequest
{
    /// <summary>
    /// Gets or sets the children of this action. Only present in VS scenarios.
    /// </summary>
    [JsonProperty(PropertyName = "_vs_group", NullValueHandling = NullValueHandling.Ignore)]
    public string? Group { get; set; }

    /// <summary>
    /// Gets or sets the priority level of the code action. Only present in VS scenarios.
    /// </summary>
    [JsonProperty(PropertyName = "_vs_priority", NullValueHandling = NullValueHandling.Ignore)]
    public RazorCodeActionPriorityLevel? Priority { get; set; }

    /// <summary>
    /// Gets or sets the range of the span this action is applicable to. Only present in VS scenarios.
    /// </summary>
    [JsonProperty(PropertyName = "_vs_applicableRange", NullValueHandling = NullValueHandling.Ignore)]
    public Range? ApplicableRange { get; set; }

    /// <summary>
    /// Gets or sets the children of this action. Only present in VS scenarios.
    /// </summary>
    [JsonProperty(PropertyName = "_vs_children", NullValueHandling = NullValueHandling.Ignore)]
    public RazorCodeAction[]? Children { get; set; }

    /// <summary>
    /// Gets or sets the telemetry id of this action. Only present in VS scenarios.
    /// </summary>
    [JsonProperty(PropertyName = "_vs_telemetryId", NullValueHandling = NullValueHandling.Ignore)]
    public Guid? TelemetryId { get; set; }

    /// <summary>
    /// Used internally by the Razor Language Server to store the Code Action name extracted
    /// from the Data.CustomTags payload.
    /// </summary>
    [JsonProperty(PropertyName = "name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }
}
