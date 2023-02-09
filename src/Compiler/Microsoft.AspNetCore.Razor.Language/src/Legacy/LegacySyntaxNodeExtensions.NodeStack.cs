// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal static partial class LegacySyntaxNodeExtensions
{
    private struct NodeStack : IDisposable
    {
        private static readonly ObjectPool<SyntaxNode[]> s_stackPool = new(() => new SyntaxNode[64]);

        private readonly SyntaxNode[] _stack;
        private int _stackPtr;

        public NodeStack(IEnumerable<SyntaxNode> nodes)
        {
            _stack = s_stackPool.Allocate();
            _stackPtr = -1;

            foreach (var node in nodes)
            {
                if (++_stackPtr == _stack.Length)
                {
                    Array.Resize(ref _stack, _stack.Length * 2);
                }

                _stack[_stackPtr] = node;
            }
        }

        public SyntaxNode Pop()
            => _stack[_stackPtr--];

        public bool IsEmpty
            => _stackPtr < 0;

        public void Dispose()
        {
            // Return only reasonably-sized stacks to the pool.
            if (_stack.Length < 256)
            {
                Array.Clear(_stack, 0, _stack.Length);
                s_stackPool.Free(_stack);
            }
        }
    }
}
