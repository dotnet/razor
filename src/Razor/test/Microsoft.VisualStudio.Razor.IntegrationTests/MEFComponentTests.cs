﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

public class MEFComponentTests(ITestOutputHelper testOutputHelper) : AbstractRazorEditorTest(testOutputHelper)
{
    [IdeFact]
    public static void EnsureCompositionCreation()
    {
        var hiveDirectory = VisualStudioLogging.GetHiveDirectory();
        var cmcPath = Path.Combine(hiveDirectory, "ComponentModelCache");

        Assert.True(Directory.Exists(cmcPath), "ComponentModelCache directory doesn't exist");

        var mefErrorFile = Path.Combine(cmcPath, "Microsoft.VisualStudio.Default.err");

        Assert.True(File.Exists(mefErrorFile), "Expected ComponentModelCache error file to exist");

        var section = new StringBuilder();
        foreach (var line in File.ReadLines(mefErrorFile))
        {
            // Individual errors are separated by a blank lines, or with an "Error #" header
            if (line.StartsWith("Error #") ||
                string.IsNullOrWhiteSpace(line))
            {
                var error = section.ToString();
                section.Clear();

                if (error.Contains("Razor"))
                {
                    Assert.True(IsAllowedFailure(error), "Unexpected MEF failure: " + line);
                }
            }
            else if (line.Equals("----------- Used assemblies -----------"))
            {
                // Finished processing errors
                break;
            }
            else
            {
                section.AppendLine(line);
            }
        }
    }

    private static bool IsAllowedFailure(string error)
    {
        return
            // We have a little issue with LiveShare at the moment. Doesn't seem to affect user scenarios
            error.Contains("Microsoft.VisualStudio.LiveShare.Razor.LiveShareProjectCapabilityResolver") ||
            error.Contains("Microsoft.VisualStudio.LiveShare.Razor.Guest.GuestProjectPathProvider") ||
            error.Contains("Microsoft.VisualStudio.LiveShare.Razor.Guest.LiveShareSessionAccessor");
    }
}
