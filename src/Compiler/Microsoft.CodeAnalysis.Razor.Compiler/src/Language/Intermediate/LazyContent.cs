// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

internal abstract class LazyContent
{
    public static LazyContent Create<T>(Func<T, string> contentCreater, T arg)
        => new LazyContentImpl<T>(contentCreater, arg);

    public abstract string Content { get; }

    private sealed class LazyContentImpl<T>(Func<T, string> contentCreater, T arg) : LazyContent
    {
        private T _arg = arg;
        private Func<T, string> _contentCreater = contentCreater;

        private string? _content;

        public override string Content
        {
            get
            {
                return _content ??= ComputeContent();

                string ComputeContent()
                {
                    var content = _contentCreater(_arg);

                    // Clear out references to allow the arg and factory to be GC'd.
                    _arg = default!;
                    _contentCreater = default!;

                    return content;
                }
            }
        }
    }
}
