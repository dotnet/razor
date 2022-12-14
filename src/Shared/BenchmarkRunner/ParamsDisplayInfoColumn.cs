﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace BenchmarkDotNet.Attributes;

public class ParamsSummaryColumn : IColumn
{
    public string Id => nameof(ParamsSummaryColumn);
    public string ColumnName { get; } = "Params";
    public bool IsDefault(Summary summary, BenchmarkCase benchmark) => false;
    public string GetValue(Summary summary, BenchmarkCase benchmark) => benchmark.Parameters.DisplayInfo;
    public bool IsAvailable(Summary summary) => true;
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Params;
    public int PriorityInCategory => 0;
    public override string ToString() => ColumnName;
    public bool IsNumeric => false;
    public UnitType UnitType => UnitType.Dimensionless;
    public string GetValue(Summary summary, BenchmarkCase benchmark, SummaryStyle style) => GetValue(summary, benchmark);
    public string Legend => $"Summary of all parameter values";
}
