// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.Razor;

internal interface ISettingsPersistenceService
{
    [return: MaybeNull]
    T GetValueOrDefault<T>(string name, [AllowNull] T defaultValue = default);
}
