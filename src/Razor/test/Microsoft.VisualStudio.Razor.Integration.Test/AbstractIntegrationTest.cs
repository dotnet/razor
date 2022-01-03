// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Extensibility.Testing;
using Xunit;
using Xunit.Sdk;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.Razor.Integration.Test
{
    // TODO: Start collecting LogFiles on failure

    /// <remarks>
    /// The following is the xunit execution order:
    ///
    /// <list type="number">
    /// <item><description>Instance constructor</description></item>
    /// <item><description><see cref="IAsyncLifetime.InitializeAsync"/></description></item>
    /// <item><description><see cref="BeforeAfterTestAttribute.Before"/></description></item>
    /// <item><description>Test method</description></item>
    /// <item><description><see cref="BeforeAfterTestAttribute.After"/></description></item>
    /// <item><description><see cref="IAsyncLifetime.DisposeAsync"/></description></item>
    /// <item><description><see cref="IDisposable.Dispose"/></description></item>
    /// </list>
    /// </remarks>
    [IdeSettings(MinVersion = VisualStudioVersion.VS2022, RootSuffix = "RoslynDev")]
    public abstract class AbstractIntegrationTest : AbstractIdeIntegrationTest
    {
        protected const string ProjectName = "TestProj";
        protected const string SolutionName = "TestSolution";

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
        }
    }
}
