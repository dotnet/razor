// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;

namespace Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;

internal static class MockExtensions
{
    public static void RaiseChanged(this Mock<IProjectSnapshotManager> mock, ProjectChangeEventArgs e)
    {
        mock.Raise(x => x.Changed += delegate { }, e);
    }
}
