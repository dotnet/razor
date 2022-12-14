﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Validators;

namespace BenchmarkDotNet.Attributes;

internal class DefaultCorePerfLabConfig : ManualConfig
{
    public DefaultCorePerfLabConfig()
    {
        AddLogger(ConsoleLogger.Default);

        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(StatisticColumn.OperationsPerSecond);
        AddColumn(new ParamsSummaryColumn());
        AddColumnProvider(DefaultColumnProviders.Statistics, DefaultColumnProviders.Metrics, DefaultColumnProviders.Descriptor);

        AddValidator(JitOptimizationsValidator.FailOnError);

        AddJob(Job.InProcess
            .WithStrategy(RunStrategy.Throughput));

        AddExporter(MarkdownExporter.GitHub);

        AddExporter(new CsvExporter(
            CsvSeparator.Comma,
            new Reports.SummaryStyle(cultureInfo: null, printUnitsInHeader: true, printUnitsInContent: false, timeUnit: Perfolizer.Horology.TimeUnit.Microsecond, sizeUnit: SizeUnit.KB)));
    }
}
