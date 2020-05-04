// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Nerdbank.Streams;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    public class AutoFlushingStreamTest
    {
        public AutoFlushingStreamTest()
        {
        }


        [Fact]
        public async void ParallelReadWrite_ClientServer()
        {
            // Arrange
            var (clientStream, serverStream) = FullDuplexStream.CreatePair();
            var autoFlushingStream = new AutoFlushingStream(serverStream);
            const int INIT_BYTES = 10000;
            var fileContents = new byte[INIT_BYTES];
            var randomGenerator = new Random();
            randomGenerator.NextBytes(fileContents);
            await clientStream.WriteAsync(fileContents, 0, INIT_BYTES);
            await clientStream.FlushAsync();
            await serverStream.WriteAsync(fileContents, 0, INIT_BYTES);
            await serverStream.FlushAsync();

            // Act
            var serverReads = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < 100000; i++)
                {
                    var tmpBuffer = new byte[10];
                    try
                    {
                        var result = autoFlushingStream.Read(tmpBuffer, 0, 10);
                        Assert.Equal(10, result);
                    }
                    catch (Exception e)
                    {
                        Assert.True(false);
                    }
                }
            });
            var serverWrites = Task.Factory.StartNew(() =>
            {
                for (var i = 0; i < 100000; i++)
                {
                    try
                    {
                        var tmpBuffer = new byte[10];
                        randomGenerator.NextBytes(tmpBuffer);
                        autoFlushingStream.Write(tmpBuffer, 0, 10);
                    }
                    catch (Exception e)
                    {
                        Assert.True(false);
                    }
                }
            });

            var clientReads = Task.Factory.StartNew(async () =>
            {
                for (var i = 0; i < 100000; i++)
                {
                    try
                    {
                        var tmpBuffer = new byte[10];
                        var result = await clientStream.ReadAsync(tmpBuffer, 0, 10);
                        Assert.Equal(10, result);
                    }
                    catch (Exception e)
                    {
                        Assert.True(false);
                    }
                }
            });
            var clientWrites = Task.Factory.StartNew(async () =>
            {
                for (var i = 0; i < 100000; i++)
                {
                    try
                    {
                        var tmpBuffer = new byte[10];
                        randomGenerator.NextBytes(tmpBuffer);
                        await clientStream.WriteAsync(tmpBuffer, 0, 10);
                        await clientStream.FlushAsync();
                    }
                    catch (Exception e)
                    {
                        Assert.True(false);
                    }
                }
            });

            // Assert
            Task.WaitAll(new[] { serverReads, serverWrites, clientReads, clientWrites });
        }

        [Fact]
        public async void ParallelReadWrite_ClientServer_Record()
        {
            // Arrange
            var (clientStream, serverStream) = FullDuplexStream.CreatePair();
            var autoFlushingStream = new AutoFlushingStream(serverStream);
            const int INIT_BYTES = 10000;
            var fileContents = new byte[INIT_BYTES];
            var randomGenerator = new Random();
            randomGenerator.NextBytes(fileContents);
            await clientStream.WriteAsync(fileContents, 0, INIT_BYTES);
            await clientStream.FlushAsync();
            await serverStream.WriteAsync(fileContents, 0, INIT_BYTES);
            await serverStream.FlushAsync();

            // Act
            var serverReads = Task.Factory.StartNew(() =>
            {
                var exception = Record.Exception(() =>
                {
                    for (var i = 0; i < 100000; i++)
                    {
                        var tmpBuffer = new byte[10];
                        var result = autoFlushingStream.Read(tmpBuffer, 0, 10);
                        Assert.Equal(10, result);
                    }
                });
                Assert.Null(exception);
            });
            var serverWrites = Task.Factory.StartNew(() =>
            {
                var exception = Record.Exception(() =>
                {
                    for (var i = 0; i < 100000; i++)
                    {
                        var tmpBuffer = new byte[10];
                        randomGenerator.NextBytes(tmpBuffer);
                        autoFlushingStream.Write(tmpBuffer, 0, 10);
                    }
                });
                Assert.Null(exception);
            });

            var clientReads = Task.Factory.StartNew(async () =>
            {
                var exception = await Record.ExceptionAsync(async () =>
                {
                    for (var i = 0; i < 100000; i++)
                    {
                        var tmpBuffer = new byte[10];
                        var result = await clientStream.ReadAsync(tmpBuffer, 0, 10);
                        Assert.Equal(10, result);
                    }
                });
                Assert.Null(exception);
            });
            var clientWrites = Task.Factory.StartNew(async () =>
            {
                var exception = await Record.ExceptionAsync(async () =>
                {
                    for (var i = 0; i < 100000; i++)
                    {
                        var tmpBuffer = new byte[10];
                        randomGenerator.NextBytes(tmpBuffer);
                        await clientStream.WriteAsync(tmpBuffer, 0, 10);
                        await clientStream.FlushAsync();
                    }
                });
                Assert.Null(exception);
            });

            Task.WaitAll(new[] { serverReads, serverWrites, clientReads, clientWrites });
        }
    }
}
