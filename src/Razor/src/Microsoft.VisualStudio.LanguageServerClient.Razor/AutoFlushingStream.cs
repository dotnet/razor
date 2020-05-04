// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal class AutoFlushingStream : Stream
    {
        private readonly Stream _inner;

        private Dictionary<(string, int), int> prevThreads = new Dictionary<(string, int), int>();

        public AutoFlushingStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Flush()
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            CheckThreads("flush");

            await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private void CheckThreads(string operation)
        {
            if (prevThreads.TryGetValue((operation, Thread.CurrentThread.ManagedThreadId), out _))
            {
                prevThreads[(operation, Thread.CurrentThread.ManagedThreadId)] += 1;
            }
            else
            {
                prevThreads[(operation, Thread.CurrentThread.ManagedThreadId)] = 1;
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckThreads("read");

            return await _inner.ReadAsync(buffer, offset, count).ConfigureAwait(false);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckThreads("write");

            await _inner.WriteAsync(buffer, offset, count).ConfigureAwait(false);
            await FlushAsync().ConfigureAwait(false);
        }
    }
}
