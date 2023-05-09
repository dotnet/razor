﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal static partial class LegacySyntaxNodeExtensions
{
    /// <summary>
    ///  This is similar to <see cref="SyntaxNode.ChildSyntaxListEnumeratorStack"/>.
    ///  However, instead of enumerating descendant nodes in a top-down, left-to-right
    ///  fashion, the process is reversed; it operates right-to-left and bottom-up.
    /// </summary>
    private partial struct ChildSyntaxListReversedEnumeratorStack : IDisposable
    {
        private const int MaxArraySize = 256;

        private static readonly ObjectPool<ChildSyntaxList.Reversed.Enumerator[]> s_stackPool = DefaultPool.Create(Policy.Instance);

        private ChildSyntaxList.Reversed.Enumerator[] _stack;
        private int _stackPtr;

        public ChildSyntaxListReversedEnumeratorStack(SyntaxNode node)
        {
            _stack = s_stackPool.Get();
            _stackPtr = -1;

            PushRightmostChildren(node);
        }

        private void PushRightmostChildren(SyntaxNode node)
        {
            var current = node;
            do
            {
                var children = current.ChildNodes();
                if (children.Count == 0)
                {
                    break;
                }

                if (++_stackPtr == _stack.Length)
                {
                    Array.Resize(ref _stack, _stack.Length * 2);
                }

                _stack[_stackPtr] = children.Reverse().GetEnumerator();

                current = children.Last();
            }
            while (current is not null);
        }

        private bool TryMoveNextAndGetCurrent([NotNullWhen(true)] out SyntaxNode? node)
        {
            if (_stackPtr < 0)
            {
                node = null;
                return false;
            }

            ref var enumerator = ref _stack[_stackPtr];

            if (!enumerator.MoveNext())
            {
                node = null;
                return false;
            }

            node = enumerator.Current;
            return true;
        }

        public bool TryGetNextNode([NotNullWhen(true)] out SyntaxNode? node)
        {
            while (!TryMoveNextAndGetCurrent(out node))
            {
                _stackPtr--;

                if (_stackPtr < 0)
                {
                    node = null;
                    return false;
                }
            }

            PushRightmostChildren(node);
            return true;
        }

        public bool IsEmpty
            => _stackPtr < 0;

        public void Dispose()
        {
            s_stackPool.Return(_stack);
        }
    }
}
