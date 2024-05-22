﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Hosting;

/// <summary>
/// Specifies the name of a Razor extension as defined by the Razor SDK.
/// </summary>
/// <remarks>
/// This attribute is applied to an application's entry point assembly by the Razor SDK during the build,
/// so that the Razor configuration can be loaded at runtime based on the settings provided by the project
/// file.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class RazorExtensionAssemblyNameAttribute : Attribute
{
    /// <summary>
    /// Creates a new instance of <see cref="RazorExtensionAssemblyNameAttribute"/>.
    /// </summary>
    /// <param name="extensionName">The name of the extension.</param>
    /// <param name="assemblyName">The assembly name of the extension.</param>
    public RazorExtensionAssemblyNameAttribute(string extensionName, string assemblyName)
    {
        if (extensionName is null)
        {
            throw new ArgumentNullException(nameof(extensionName));
        }

        if (assemblyName is null)
        {
            throw new ArgumentNullException(nameof(assemblyName));
        }

        ExtensionName = extensionName;
        AssemblyName = assemblyName;
    }

    /// <summary>
    /// Gets the assembly name of the extension.
    /// </summary>
    public string AssemblyName { get; }

    /// <summary>
    /// Gets the name of the extension.
    /// </summary>
    public string ExtensionName { get; }
}