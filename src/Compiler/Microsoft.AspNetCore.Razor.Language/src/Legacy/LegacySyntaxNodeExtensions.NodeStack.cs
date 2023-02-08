// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal static partial class LegacySyntaxNodeExtensions
{
    private struct NodeStack : IDisposable
    {
        [ThreadStatic]
        private static SyntaxNode[]? g_nodeStack;

        private readonly SyntaxNode[] _stack;
        private int _stackPtr;

        public NodeStack(IEnumerable<SyntaxNode> nodes)
        {
            _stack = g_nodeStack ??= new SyntaxNode[64];
            _stackPtr = -1;

            foreach (var node in nodes)
            {
                if (++_stackPtr == _stack.Length)
                {
                    Array.Resize(ref _stack, _stack.Length * 2);
                    g_nodeStack = _stack;
                }

                _stack[_stackPtr] = node;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SyntaxNode Pop()
            => _stack[_stackPtr--];

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _stackPtr < 0;
        }

        public void Dispose()
            => Array.Clear(_stack, 0, _stack.Length);
    }
}
