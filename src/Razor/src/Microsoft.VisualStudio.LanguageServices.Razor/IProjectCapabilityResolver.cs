// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.Razor;

internal interface IProjectCapabilityResolver
{
    public bool HasCapability(object project, string capability);

    public bool HasCapability(string documentFilePath, object project, string capability);
}
