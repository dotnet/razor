﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LiveShare.Razor.Guest;

public class RazorGuestInitializationServiceTest : TestBase
{
    private readonly DefaultLiveShareSessionAccessor _liveShareSessionAccessor;

    public RazorGuestInitializationServiceTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _liveShareSessionAccessor = new DefaultLiveShareSessionAccessor();
    }

    [Fact]
    public async Task CreateServiceAsync_StartsViewImportsCopy()
    {
        // Arrange
        var service = new RazorGuestInitializationService(_liveShareSessionAccessor);
        var session = new Mock<CollaborationSession>(MockBehavior.Strict);
        session.Setup(s => s.ListRootsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Uri>())
            .Verifiable();

        // Act
        await service.CreateServiceAsync(session.Object, default);

        // Assert
        Assert.NotNull(service._viewImportsCopyTask);
        await service._viewImportsCopyTask;

        session.VerifyAll();
    }

    [Fact]
    public async Task CreateServiceAsync_SessionDispose_CancelsListRootsToken()
    {
        // Arrange
        var service = new RazorGuestInitializationService(_liveShareSessionAccessor);
        var session = new Mock<CollaborationSession>(MockBehavior.Strict);
        using var disposedServiceGate = new ManualResetEventSlim();
        var disposedService = false;
        IDisposable sessionService = null;
        session.Setup(s => s.ListRootsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>((cancellationToken) => Task.Run(() =>
                {
                    cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));

                    // Make sure we don't assert the value of 'disposedService' before we know it was set
                    disposedServiceGate.Wait();

                    Assert.True(disposedService);
                    return Array.Empty<Uri>();
                }))
            .Verifiable();
        sessionService = (IDisposable)await service.CreateServiceAsync(session.Object, default);

        // Act
        sessionService.Dispose();
        disposedService = true;
        disposedServiceGate.Set();

        // Assert
        Assert.NotNull(service._viewImportsCopyTask);
        await service._viewImportsCopyTask;

        session.VerifyAll();
    }

    [Fact]
    public async Task CreateServiceAsync_InitializationDispose_CancelsListRootsToken()
    {
        // Arrange
        var service = new RazorGuestInitializationService(_liveShareSessionAccessor);
        var session = new Mock<CollaborationSession>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        IDisposable sessionService = null;
        session.Setup(s => s.ListRootsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>((cancellationToken) => Task.Run(() =>
                {
                    cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(3));

                    Assert.True(cts.IsCancellationRequested);
                    return Array.Empty<Uri>();
                }))
            .Verifiable();
        sessionService = (IDisposable)await service.CreateServiceAsync(session.Object, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        Assert.NotNull(service._viewImportsCopyTask);
        await service._viewImportsCopyTask;

        session.VerifyAll();
    }

    [Fact]
    public async Task CreateServiceAsync_EnsureViewImportsCopiedAsync_CancellationExceptionsGetSwallowed()
    {
        // Arrange
        var service = new RazorGuestInitializationService(_liveShareSessionAccessor);
        var session = new Mock<CollaborationSession>(MockBehavior.Strict);
        using var cts = new CancellationTokenSource();
        IDisposable sessionService = null;
        session.Setup(s => s.ListRootsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>((cancellationToken) => Task.Run(() =>
                {
                    cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(3));

                    cancellationToken.ThrowIfCancellationRequested();

                    return Array.Empty<Uri>();
                }))
            .Verifiable();
        sessionService = (IDisposable)await service.CreateServiceAsync(session.Object, cts.Token);

        // Act
        cts.Cancel();

        // Assert
        Assert.NotNull(service._viewImportsCopyTask);
        await service._viewImportsCopyTask;

        session.VerifyAll();
    }
}
