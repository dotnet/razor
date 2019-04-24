// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class ApplicationPartsProviderTest
    {
        internal static readonly string[] MvcAssemblies = new[]
        {
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Mvc.Abstractions",
            "Microsoft.AspNetCore.Mvc.ApiExplorer",
            "Microsoft.AspNetCore.Mvc.Core",
            "Microsoft.AspNetCore.Mvc.Cors",
            "Microsoft.AspNetCore.Mvc.DataAnnotations",
            "Microsoft.AspNetCore.Mvc.Formatters.Json",
            "Microsoft.AspNetCore.Mvc.Formatters.Xml",
            "Microsoft.AspNetCore.Mvc.Localization",
            "Microsoft.AspNetCore.Mvc.NewtonsoftJson",
            "Microsoft.AspNetCore.Mvc.Razor",
            "Microsoft.AspNetCore.Mvc.RazorPages",
            "Microsoft.AspNetCore.Mvc.TagHelpers",
            "Microsoft.AspNetCore.Mvc.ViewFeatures",
        };

        [Fact]
        public void Resolve_ReturnsEmptySequence_IfNoAssemblyReferencesMvc()
        {
            // Arrange
            var provider = new TestApplicationPartsProvider(new[]
            {
                CreateReferenceItem("Microsoft.AspNetCore.Blazor"),
                CreateReferenceItem("Microsoft.AspNetCore.Components"),
                CreateReferenceItem("Microsoft.JSInterop"),
                CreateReferenceItem("System.Net.Http"),
                CreateReferenceItem("System.Runtime"),
            });

            provider.Add("Microsoft.AspNetCore.Blazor", "Microsoft.AspNetCore.Components", "Microsoft.JSInterop");
            provider.Add("Microsoft.AspNetCore.Components", "Microsoft.JSInterop", "System.Net.Http", "System.Runtime");
            provider.Add("System.Net.Http", "System.Runtime");

            // Act
            var assemblies = provider.ResolveAssemblies();

            // Assert
            Assert.Empty(assemblies);
        }

        [Fact]
        public void Resolve_ReturnsEmptySequence_IfNoDependencyReferencesMvc()
        {
            // Arrange
            var provider = new TestApplicationPartsProvider(new[]
            {
                CreateReferenceItem("MyApp.Models"),
                CreateReferenceItem("Microsoft.AspNetCore.Mvc", isSystemReference: true),
                CreateReferenceItem("Microsoft.AspNetCore.Hosting", isSystemReference: true),
                CreateReferenceItem("Microsoft.AspNetCore.HttpAbstractions", isSystemReference: true),
                CreateReferenceItem("Microsoft.AspNetCore.KestrelHttpServer", isSystemReference: true),
                CreateReferenceItem("Microsoft.AspNetCore.StaticFiles", isSystemReference: true),
                CreateReferenceItem("Microsoft.Extensions.Primitives", isSystemReference: true),
                CreateReferenceItem("System.Net.Http", isSystemReference: true),
                CreateReferenceItem("Microsoft.EntityFrameworkCore"),
            });

            provider.Add("MyApp.Models", "Microsoft.EntityFrameworkCore");
            provider.Add("Microsoft.AspNetCore.Mvc", "Microsoft.AspNetCore.HttpAbstractions");
            provider.Add("Microsoft.AspNetCore.KestrelHttpServer", "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.HttpAbstractions");
            provider.Add("Microsoft.AspNetCore.StaticFiles", "Microsoft.AspNetCore.HttpAbstractions", "Microsoft.Extensions.Primitives");
            provider.Add("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.HttpAbstractions");
            provider.Add("Microsoft.AspNetCore.HttpAbstractions", "Microsoft.Extensions.Primitives");

            // Act
            var assemblies = provider.ResolveAssemblies();

            // Assert
            Assert.Empty(assemblies);
        }

        [Fact]
        public void Resolve_ReturnsReferences_ThatReferenceMvc()
        {
            // Arrange
            var provider = new TestApplicationPartsProvider(new[]
            {
                CreateReferenceItem("Microsoft.AspNetCore.Mvc", isSystemReference: true),
                CreateReferenceItem("Microsoft.AspNetCore.Mvc.TagHelpers", isSystemReference: true),
                CreateReferenceItem("MyTagHelpers"),
                CreateReferenceItem("MyControllers"),
                CreateReferenceItem("MyApp.Models"),
                CreateReferenceItem("Microsoft.AspNetCore.Hosting", isSystemReference: true),
                CreateReferenceItem("Microsoft.AspNetCore.HttpAbstractions", isSystemReference: true),
                CreateReferenceItem("Microsoft.AspNetCore.KestrelHttpServer", isSystemReference: true),
                CreateReferenceItem("Microsoft.AspNetCore.StaticFiles", isSystemReference: true),
                CreateReferenceItem("Microsoft.Extensions.Primitives", isSystemReference: true),
                CreateReferenceItem("Microsoft.EntityFrameworkCore"),
            });

            provider.Add("MyTagHelpers", "Microsoft.AspNetCore.Mvc.TagHelpers");
            provider.Add("MyControllers", "Microsoft.AspNetCore.Mvc");
            provider.Add("MyApp.Models", "Microsoft.EntityFrameworkCore");
            provider.Add("Microsoft.AspNetCore.Mvc", "Microsoft.AspNetCore.HttpAbstractions", "Microsoft.AspNetCore.Mvc.TagHelpers");
            provider.Add("Microsoft.AspNetCore.KestrelHttpServer", "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.HttpAbstractions");
            provider.Add("Microsoft.AspNetCore.StaticFiles", "Microsoft.AspNetCore.HttpAbstractions", "Microsoft.Extensions.Primitives");
            provider.Add("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.HttpAbstractions");
            provider.Add("Microsoft.AspNetCore.HttpAbstractions", "Microsoft.Extensions.Primitives");

            // Act
            var assemblies = provider.ResolveAssemblies();

            // Assert
            Assert.Equal(new[] { "MyControllers", "MyTagHelpers" }, assemblies.OrderBy(a => a));
        }

        [Fact]
        public void Resolve_ReturnsItemsThatTransitivelyReferenceMvc()
        {
            // Arrange
            var provider = new TestApplicationPartsProvider(new[]
            {
                CreateReferenceItem("MyCMS"),
                CreateReferenceItem("MyCMS.Core"),
                CreateReferenceItem("Microsoft.AspNetCore.Mvc.ViewFeatures", isSystemReference: true),
            });

            provider.Add("MyCMS", "MyCMS.Core");
            provider.Add("MyCMS.Core", "Microsoft.AspNetCore.Mvc.ViewFeatures");


            // Act
            var assemblies = provider.ResolveAssemblies();

            // Assert
            Assert.Equal(new[] { "MyCMS", "MyCMS.Core" }, assemblies.OrderBy(a => a));
        }

        public ResolveReferenceItem CreateReferenceItem(string name, bool isSystemReference = false)
        {
            return new ResolveReferenceItem
            {
                AssemblyName = name,
                IsSystemReference = isSystemReference,
                Path = name,
            };
        }

        private class TestApplicationPartsProvider : ApplicationPartsProvider
        {
            private readonly Dictionary<string, List<AssemblyItem>> _references = new Dictionary<string, List<AssemblyItem>>();
            private readonly Dictionary<string, AssemblyItem> _lookup;

            public TestApplicationPartsProvider(ResolveReferenceItem[] referenceItems)
                : base(MvcAssemblies, referenceItems)
            {
                _lookup = referenceItems.ToDictionary(r => r.AssemblyName, r => new AssemblyItem(r));
            }

            public void Add(string assembly, params string[] references)
            {
                var assemblyItems = references.Select(r => _lookup[r]).ToList();
                _references[assembly] = assemblyItems;
            }


            protected override IReadOnlyList<AssemblyItem> GetReferences(string file)
            {
                if (_references.TryGetValue(file, out var result))
                {
                    return result;
                }

                return Array.Empty<AssemblyItem>();
            }
        }
    }
}
