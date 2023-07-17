// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Test;

public class ChecksumTests(ITestOutputHelper testOutput) : TestBase(testOutput)
{
    public static IEnumerable<object[]> Checksums
    {
        get
        {
            yield return new object[] { true, s_empty, s_empty };
            yield return new object[] { true, s_falseValue, s_falseValue };
            yield return new object[] { false, s_falseValue, s_trueValue };
            yield return new object[] { false, s_trueValue, s_falseValue };
            yield return new object[] { true, s_trueValue, s_trueValue };
            yield return new object[] { true, CreateIntArray(new[] { 1, 2, 3, 4, 5 }), CreateIntArray(new[] { 1, 2, 3, 4, 5 }) };
            yield return new object[] { false, CreateIntArray(new[] { 1, 2, 3, 4, 5 }), CreateIntArray(new[] { 5, 4, 3, 2, 1 }) };
        }
    }

    private static readonly Func<Checksum> s_empty = () =>
    {
        var builder = new Checksum.Builder();
        return builder.FreeAndGetChecksum();
    };

    private static readonly Func<Checksum> s_falseValue = () =>
    {
        var builder = new Checksum.Builder();
        builder.AppendData(false);
        return builder.FreeAndGetChecksum();
    };

    private static readonly Func<Checksum> s_trueValue = () =>
    {
        var builder = new Checksum.Builder();
        builder.AppendData(true);
        return builder.FreeAndGetChecksum();
    };

    private static Func<Checksum> CreateIntArray(int[] values)
    {
        return () =>
        {
            var builder = new Checksum.Builder();
            builder.AppendData(values);
            return builder.FreeAndGetChecksum();
        };
    }

    [Theory]
    [MemberData(nameof(Checksums))]
    internal void TestEquality(bool areEqual, Func<Checksum> producer1, Func<Checksum> producer2)
    {
        var checksum1 = producer1();
        var checksum2 = producer2();

        if (areEqual)
        {
            Assert.Equal(checksum1, checksum2);
        }
        else
        {
            Assert.NotEqual(checksum1, checksum2);
        }
    }
}
