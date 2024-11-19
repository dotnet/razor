// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Uncomment to easily generate baselines for tests
//#define GENERATE_JSON_FILES

using Xunit;

public static class GenerateJsonFiles
{
#if GENERATE_JSON_FILES
    internal static readonly bool ShouldGenerate = true;
#else
    internal static readonly bool ShouldGenerate = false;
#endif

    // This is to prevent you from accidentally checking in with GenerateJsonFiles = true
    [Fact]
    public static void GenerateJsonFilesMustBeFalse()
    {
        Assert.False(ShouldGenerate, "GenerateJsonFiles should be set back to false before you check in!");
    }
}
