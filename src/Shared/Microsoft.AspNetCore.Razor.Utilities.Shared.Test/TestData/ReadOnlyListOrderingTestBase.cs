﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Utilities.Shared.Test.TestData;

public abstract class ReadOnlyListOrderingTestBase : OrderingTestBase<IReadOnlyList<int>, IReadOnlyList<ValueHolder<int>>, OrderingCaseConverters.ReadOnlyList>
{
}
