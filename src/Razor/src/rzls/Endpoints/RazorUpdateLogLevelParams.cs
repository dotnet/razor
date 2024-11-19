// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Endpoints;

/// <summary>
/// Request parameters for updating the log level in the server dynamically.
/// </summary>
/// <param name="LogLevelValue">the string value of the <see cref="LogLevel"/> enum</param>
internal record class UpdateLogLevelParams([property: JsonPropertyName("logLevel")] string LogLevelValue);
