// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis.Razor;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;

public abstract class VisualStudioWorkspaceTestBase(ITestOutputHelper testOutput) : WorkspaceTestBase(testOutput)
{
    private protected override ProjectSnapshotManagerDispatcher CreateDispatcher()
    {
        var dispatcher = new ProjectSnapshotManagerDispatcher(ErrorReporter);
        AddDisposable(dispatcher);

        return dispatcher;
    }
}
