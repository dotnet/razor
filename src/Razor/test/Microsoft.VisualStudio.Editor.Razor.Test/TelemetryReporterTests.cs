// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.VisualStudio.Editor.Razor.Test.Shared;
using Microsoft.VisualStudio.Telemetry;
using Xunit;

namespace Microsoft.VisualStudio.Editor.Razor.Test;
public class TelemetryReporterTests
{
    [Fact]
    public void NoArgument()
    {
        var reporter = new TestTelemetryReporter();
        reporter.ReportEvent("EventName", Severity.Normal);
        Assert.Collection(reporter.Events,
            e1 =>
            {
                Assert.Equal(TelemetrySeverity.Normal, e1.Severity);
                Assert.Equal("dotnet/razor/eventname", e1.Name);
                Assert.False(e1.HasProperties);
            });
    }

    [Fact]
    public void OneArgument()
    {
        var reporter = new TestTelemetryReporter();
        reporter.ReportEvent("EventName", Severity.Normal, new Property("P1", false));
        Assert.Collection(reporter.Events,
            e1 =>
            {
                Assert.Equal(TelemetrySeverity.Normal, e1.Severity);
                Assert.Equal("dotnet/razor/eventname", e1.Name);
                Assert.True(e1.HasProperties);
                Assert.Single(e1.Properties);

                Assert.Equal(false, e1.Properties["dotnet.razor.p1"]);
            });
    }

    [Fact]
    public void TwoArguments()
    {
        var reporter = new TestTelemetryReporter();
        reporter.ReportEvent("EventName", Severity.Normal, new("P1", false), new("P2", "test"));
        Assert.Collection(reporter.Events,
            e1 =>
            {
                Assert.Equal(TelemetrySeverity.Normal, e1.Severity);
                Assert.Equal("dotnet/razor/eventname", e1.Name);
                Assert.True(e1.HasProperties);

                Assert.Equal(false, e1.Properties["dotnet.razor.p1"]);
                Assert.Equal("test", e1.Properties["dotnet.razor.p2"]);
            });
    }

    [Fact]
    public void ThreeArguments()
    {
        var reporter = new TestTelemetryReporter();
        var p3Value = Guid.NewGuid();
        reporter.ReportEvent("EventName",
            Severity.Normal,
            new("P1", false),
            new("P2", "test"),
            new("P3", p3Value));

        Assert.Collection(reporter.Events,
            e1 =>
            {
                Assert.Equal(TelemetrySeverity.Normal, e1.Severity);
                Assert.Equal("dotnet/razor/eventname", e1.Name);
                Assert.True(e1.HasProperties);

                Assert.Equal(false, e1.Properties["dotnet.razor.p1"]);
                Assert.Equal("test", e1.Properties["dotnet.razor.p2"]);

                var p3 = e1.Properties["dotnet.razor.p3"] as TelemetryComplexProperty;
                Assert.NotNull(p3);
                Assert.Equal(p3Value, p3.Value);
            });
    }

    [Fact]
    public void FourArguments()
    {
        var reporter = new TestTelemetryReporter();
        var p3Value = Guid.NewGuid();
        reporter.ReportEvent("EventName",
            Severity.Normal,
            new("P1", false),
            new("P2", "test"),
            new("P3", p3Value),
            new("P4", 100));

        Assert.Collection(reporter.Events,
            e1 =>
            {
                Assert.Equal(TelemetrySeverity.Normal, e1.Severity);
                Assert.Equal("dotnet/razor/eventname", e1.Name);
                Assert.True(e1.HasProperties);

                Assert.Equal(false, e1.Properties["dotnet.razor.p1"]);
                Assert.Equal("test", e1.Properties["dotnet.razor.p2"]);

                var p3 = e1.Properties["dotnet.razor.p3"] as TelemetryComplexProperty;
                Assert.NotNull(p3);
                Assert.Equal(p3Value, p3.Value);

                Assert.Equal(100, e1.Properties["dotnet.razor.p4"]);
            });
    }
}
