﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.VisualStudio.Threading;
using Xunit.Sdk;

#if NETFRAMEWORK
using System.Windows.Threading;
#endif

#if false
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
#endif

namespace Microsoft.AspNetCore.Razor.Test.Common.Mef;

// Starting with 15.3 the editor took a dependency on JoinableTaskContext
// in Text.Logic and IntelliSense layers as an editor host provided service.
[Export]
public partial class TestExportJoinableTaskContext
{
    [ImportingConstructor]
    //[Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public TestExportJoinableTaskContext()
    {
        var synchronizationContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(GetEffectiveSynchronizationContext());
            (JoinableTaskContext, SynchronizationContext) = CreateJoinableTaskContext();
#if false
        ResetThreadAffinity(JoinableTaskContext.Factory);
#endif
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);
        }
    }

    private static (JoinableTaskContext joinableTaskContext, SynchronizationContext synchronizationContext) CreateJoinableTaskContext()
    {
        Thread mainThread;
        SynchronizationContext synchronizationContext;
#if NETFRAMEWORK
    if (SynchronizationContext.Current is DispatcherSynchronizationContext)
    {
        // The current thread is the main thread, and provides a suitable synchronization context
        mainThread = Thread.CurrentThread;
        synchronizationContext = SynchronizationContext.Current;
    }
    else
#endif
        {
            // The current thread is not known to be the main thread; we have no way to know if the
            // synchronization context of the current thread will behave in a manner consistent with main thread
            // synchronization contexts, so we use DenyExecutionSynchronizationContext to track any attempted
            // use of it.
            var denyExecutionSynchronizationContext = new DenyExecutionSynchronizationContext(SynchronizationContext.Current);
            mainThread = denyExecutionSynchronizationContext.MainThread;
            synchronizationContext = denyExecutionSynchronizationContext;
        }

        return (new JoinableTaskContext(mainThread, synchronizationContext), synchronizationContext);
    }

    [Export]
    private JoinableTaskContext JoinableTaskContext
    {
        get;
    }

    internal SynchronizationContext SynchronizationContext
    {
        get;
    }

    internal static SynchronizationContext? GetEffectiveSynchronizationContext()
    {
        if (SynchronizationContext.Current is AsyncTestSyncContext asyncTestSyncContext)
        {
            SynchronizationContext? innerSynchronizationContext = null;
            asyncTestSyncContext.Send(
                _ => innerSynchronizationContext = SynchronizationContext.Current,
                null);

            return innerSynchronizationContext;
        }
        else
        {
            return SynchronizationContext.Current;
        }
    }

#if false
/// <summary>
/// Reset the thread affinity, in particular the designated foreground thread, to the active
/// thread.
/// </summary>
internal static void ResetThreadAffinity(JoinableTaskFactory joinableTaskFactory)
{
    // HACK: When the platform team took over several of our components they created a copy
    // of ForegroundThreadAffinitizedObject.  This needs to be reset in the same way as our copy
    // does.  Reflection is the only choice at the moment.
    var thread = joinableTaskFactory.Context.MainThread;
    var taskScheduler = new JoinableTaskFactoryTaskScheduler(joinableTaskFactory);

    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
    {
        var type = assembly.GetType("Microsoft.VisualStudio.Language.Intellisense.Implementation.ForegroundThreadAffinitizedObject", throwOnError: false);
        if (type != null)
        {
            type.GetField("foregroundThread", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, thread);
            type.GetField("ForegroundTaskScheduler", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, taskScheduler);

            break;
        }
    }
}

// HACK: Part of ResetThreadAffinity
private class JoinableTaskFactoryTaskScheduler : TaskScheduler
{
    private readonly JoinableTaskFactory _joinableTaskFactory;

    public JoinableTaskFactoryTaskScheduler(JoinableTaskFactory joinableTaskFactory)
        => _joinableTaskFactory = joinableTaskFactory;

    public override int MaximumConcurrencyLevel => 1;

    protected override IEnumerable<Task>? GetScheduledTasks() => null;

    protected override void QueueTask(Task task)
    {
        _joinableTaskFactory.RunAsync(async () =>
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();
            TryExecuteTask(task);
        });
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        if (_joinableTaskFactory.Context.IsOnMainThread)
        {
            return TryExecuteTask(task);
        }

        return false;
    }
}
#endif
}
