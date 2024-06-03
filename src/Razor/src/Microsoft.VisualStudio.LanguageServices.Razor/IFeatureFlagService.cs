// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.Razor;

internal interface IFeatureFlagService
{
    bool IsFeatureEnabled(string featureName, bool defaultValue = false);
}
