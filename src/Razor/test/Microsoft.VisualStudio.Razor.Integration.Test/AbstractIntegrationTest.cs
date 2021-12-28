// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.Razor.Integration.Test.InProcess;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
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
    public abstract class AbstractIntegrationTest : IAsyncLifetime, IDisposable
    {
        protected const string ProjectName = "TestProj";
        protected const string SolutionName = "TestSolution";

        /// <summary>
        /// A long timeout used to avoid hangs in tests, where a test failure manifests as an operation never occurring.
        /// </summary>
        public static readonly TimeSpan HangMitigatingTimeout = TimeSpan.FromMinutes(4);

        private JoinableTaskContext? _joinableTaskContext;
        private JoinableTaskCollection? _joinableTaskCollection;
        private JoinableTaskFactory? _joinableTaskFactory;

        private TestServices? _testServices;

        private readonly CancellationTokenSource _hangMitigatingCancellationTokenSource;

        protected AbstractIntegrationTest()
        {
            Assert.True(Application.Current.Dispatcher.CheckAccess());

            JoinableTaskContext = ThreadHelper.JoinableTaskContext;

            _hangMitigatingCancellationTokenSource = new CancellationTokenSource(HangMitigatingTimeout);
        }

        [NotNull]
        protected JoinableTaskContext? JoinableTaskContext
        {
            get
            {
                return _joinableTaskContext ?? throw new InvalidOperationException();
            }

            private set
            {
                if (value == _joinableTaskContext)
                {
                    return;
                }

                if (value is null)
                {
                    _joinableTaskContext = null;
                    _joinableTaskCollection = null;
                    _joinableTaskFactory = null;
                }
                else
                {
                    _joinableTaskContext = value;
                    _joinableTaskCollection = value.CreateCollection();
                    _joinableTaskFactory = value.CreateFactory(_joinableTaskCollection).WithPriority(Application.Current.Dispatcher, DispatcherPriority.Background);
                }
            }
        }

        private protected TestServices TestServices
        {
            get
            {
                return _testServices ?? throw new InvalidOperationException($"{nameof(TestServices)} called before being set.");
            }

            private set
            {
                _testServices = value;
            }
        }

        protected JoinableTaskFactory JoinableTaskFactory
            => _joinableTaskFactory ?? throw new InvalidOperationException($"{nameof(JoinableTaskFactory)} called before {nameof(JoinableTaskContext)} was set.");

        protected CancellationToken HangMitigatingCancellationToken
            => _hangMitigatingCancellationTokenSource.Token;

        public virtual async Task InitializeAsync()
        {
            TestServices = await CreateTestServicesAsync();
        }

        /// <summary>
        /// This method implements <see cref="IAsyncLifetime.DisposeAsync"/>, and is used for releasing resources
        /// created by <see cref="IAsyncLifetime.InitializeAsync"/>. This method is only called if
        /// <see cref="InitializeAsync"/> completes successfully.
        /// </summary>
        public virtual async Task DisposeAsync()
        {
            if (TestServices is null)
            {
                throw new InvalidOperationException($"{nameof(DisposeAsync)} has already been called.");
            }

            await TestServices.SolutionExplorer.CloseSolutionAsync(HangMitigatingCancellationToken);

            if (_joinableTaskCollection is not null)
            {
                await _joinableTaskCollection.JoinTillEmptyAsync(HangMitigatingCancellationToken);
            }

            JoinableTaskContext = null;
        }

        /// <summary>
        /// This method provides the implementation for <see cref="IDisposable.Dispose"/>.
        /// This method is called via the <see cref="IDisposable"/> interface if the constructor completes successfully.
        /// The <see cref="InitializeAsync"/> may or may not have completed successfully.
        /// </summary>
        [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "CA1816 is invalid for the new Dispose pattern.")]
        public virtual void Dispose()
        {
        }

        private protected virtual async Task<TestServices> CreateTestServicesAsync()
            => await TestServices.CreateAsync(JoinableTaskFactory);
    }
}
