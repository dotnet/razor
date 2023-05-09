﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor;

internal interface ICustomProjectEngineFactoryMetadata
{
    string ConfigurationName { get; }

    bool SupportsSerialization { get; }
}
