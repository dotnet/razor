﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Mvc.ApplicationParts;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class ApplicationPartAttribute : Attribute
{
    public ApplicationPartAttribute(string assemblyName) { }
}
