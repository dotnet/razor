// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.AspNetCore.Razor.Design.IntegrationTests
{
    [CollectionDefinition(CollectionName)]
    public class ProjectTestCollection : ICollectionFixture<ProjectTestFixture>
    {
        public const string CollectionName = "Project test collection";
    }
}
