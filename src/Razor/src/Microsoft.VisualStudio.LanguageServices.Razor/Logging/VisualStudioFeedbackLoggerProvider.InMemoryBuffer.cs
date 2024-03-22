// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Logging;

internal partial class VisualStudioFeedbackLoggerProvider
{
    /// <summary>
    /// A circular in memory buffer to store logs in memory. The intent is
    /// to lazily allocate the buffer as needed, provide a <see cref="ToString"/> method
    /// for human readability of logs, and prepare the memory for garbage collection
    /// on call to <see cref="Clear"/>
    /// </summary>
    private class InMemoryBuffer(int bufferSize)
    {
        private Lazy<string[]> _memory = new (() => new string[bufferSize]);

        // Start at -1 because append always increments, so we want to start at value 0
        private int _head = -1;

        public void Append(string s)
        {
            var position = Interlocked.Increment(ref _head) % _memory.Value.Length;
            _memory.Value[position] = s;
        }

        public void Clear()
        {
            Interlocked.Exchange(ref _memory, new Lazy<string[]>(() => new string[bufferSize]));
        }

        public override string ToString()
        {
            var (memory, position) = (_memory.Value, _head);
            var sb = new StringBuilder(memory.Length);

            for (var i = 0; i < memory.Length; i++)
            {
                sb.AppendLine(memory[position % memory.Length]);
                position = (position + 1) % memory.Length;
            }

            return sb.ToString();
        }
    }
}
