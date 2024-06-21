// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;

#if !NET
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif

namespace Microsoft.AspNetCore.Razor.Utilities;

internal static class StreamExtensions
{
    public static Task WriteStringAsync(this Stream stream, string text, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        Debug.Assert(stream.CanWrite);
        encoding ??= Encoding.UTF8;

        var encodedBytes = encoding.GetBytes(text);

        WriteSize(stream, encodedBytes.Length);
        return stream.WriteAsync(encodedBytes, 0, encodedBytes.Length, cancellationToken);
    }

    public static async Task<string> ReadStringAsync(this Stream stream, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        Debug.Assert(stream.CanRead);
        encoding ??= Encoding.UTF8;

        var length = ReadSize(stream);
        var encodedBytes = new byte[length];
        await stream.ReadAsync(encodedBytes, 0, encodedBytes.Length, cancellationToken).ConfigureAwait(false);
        return encoding.GetString(encodedBytes);
    }

    public static ProjectInfoAction ReadProjectInfoAction(this Stream stream)
    {
        var action = stream.ReadByte();
        return action switch
        {
            0 => ProjectInfoAction.Update,
            1 => ProjectInfoAction.Remove,
            _ => throw Assumes.NotReachable()
        };
    }

    public static void WriteProjectInfoAction(this Stream stream, ProjectInfoAction projectInfoAction)
    {
        stream.WriteByte(projectInfoAction switch
        {
            ProjectInfoAction.Update => 0,
            ProjectInfoAction.Remove => 1,
            _ => throw Assumes.NotReachable()
        });
    }

    public static Task WriteProjectInfoRemovalAsync(this Stream stream, string intermediateOutputPath, CancellationToken cancellationToken)
    {
        WriteProjectInfoAction(stream, ProjectInfoAction.Remove);
        return stream.WriteStringAsync(intermediateOutputPath, cancellationToken: cancellationToken);
    }

    public static Task<string> ReadProjectInfoRemovalAsync(this Stream stream, CancellationToken cancellationToken)
    {
        return stream.ReadStringAsync(cancellationToken: cancellationToken);
    }

    public static async Task WriteProjectInfoAsync(this Stream stream, RazorProjectInfo projectInfo, CancellationToken cancellationToken)
    {
        WriteProjectInfoAction(stream, ProjectInfoAction.Update);

        var bytes = projectInfo.Serialize();
        using var _ = ArrayPool<byte>.Shared.GetPooledArray(4, out var sizeBytes);

#if NET
        BitConverter.TryWriteBytes(sizeBytes, bytes.Length);
#else
        // Pulled from https://github.com/dotnet/runtime/blob/4b9a1b2d956f4a10a28b8f5f3f725e76eb6fb826/src/libraries/System.Private.CoreLib/src/System/BitConverter.cs#L158C13-L158C87
        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(sizeBytes.AsSpan()), bytes.Length);
#endif

        await stream.WriteAsync(sizeBytes, 0, sizeBytes.Length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<RazorProjectInfo?> ReadProjectInfoAsync(this Stream stream, CancellationToken cancellationToken)
    {
        using var _ = ArrayPool<byte>.Shared.GetPooledArray(4, out var sizeBytes);
        await stream.ReadAsync(sizeBytes, 0, sizeBytes.Length, cancellationToken).ConfigureAwait(false);

        var sizeToRead = BitConverter.ToInt32(sizeBytes, 0);

        using var _1 = ArrayPool<byte>.Shared.GetPooledArray(sizeToRead, out var projectInfoBytes);
        await stream.ReadAsync(projectInfoBytes, 0, projectInfoBytes.Length, cancellationToken).ConfigureAwait(false);
        return RazorProjectInfo.DeserializeFrom(projectInfoBytes.AsMemory());
    }

    private static void WriteSize(Stream stream, int length)
    {
        var bytes = GetSizeBytes(length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static int ReadSize(Stream stream)
    {
        var bytes = new byte[4];
        stream.Read(bytes, 0, bytes.Length);

        return bytes.Sum(b => (int)b);
    }

    private static byte[] GetSizeBytes(int length)
        => BitConverter.GetBytes(length);
}
