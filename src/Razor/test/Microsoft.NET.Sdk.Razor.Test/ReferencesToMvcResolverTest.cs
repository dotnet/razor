// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class ReferencesToMvcResolverTest
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
            var resolver = new TestReferencesToMvcResolver(new[]
            {
                CreateReferenceItem("Microsoft.AspNetCore.Blazor"),
                CreateReferenceItem("Microsoft.AspNetCore.Components"),
                CreateReferenceItem("Microsoft.JSInterop"),
                CreateReferenceItem("System.Net.Http"),
                CreateReferenceItem("System.Runtime"),
            });

            resolver.Add("Microsoft.AspNetCore.Blazor", "Microsoft.AspNetCore.Components", "Microsoft.JSInterop");
            resolver.Add("Microsoft.AspNetCore.Components", "Microsoft.JSInterop", "System.Net.Http", "System.Runtime");
            resolver.Add("System.Net.Http", "System.Runtime");

            // Act
            var assemblies = resolver.ResolveAssemblies();

            // Assert
            Assert.Empty(assemblies);
        }

        [Fact]
        public void Resolve_ReturnsEmptySequence_IfNoDependencyReferencesMvc()
        {
            // Arrange
            var resolver = new TestReferencesToMvcResolver(new[]
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

            resolver.Add("MyApp.Models", "Microsoft.EntityFrameworkCore");
            resolver.Add("Microsoft.AspNetCore.Mvc", "Microsoft.AspNetCore.HttpAbstractions");
            resolver.Add("Microsoft.AspNetCore.KestrelHttpServer", "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.HttpAbstractions");
            resolver.Add("Microsoft.AspNetCore.StaticFiles", "Microsoft.AspNetCore.HttpAbstractions", "Microsoft.Extensions.Primitives");
            resolver.Add("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.HttpAbstractions");
            resolver.Add("Microsoft.AspNetCore.HttpAbstractions", "Microsoft.Extensions.Primitives");

            // Act
            var assemblies = resolver.ResolveAssemblies();

            // Assert
            Assert.Empty(assemblies);
        }

        [Fact]
        public void Resolve_ReturnsReferences_ThatReferenceMvc()
        {
            // Arrange
            var resolver = new TestReferencesToMvcResolver(new[]
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

            resolver.Add("MyTagHelpers", "Microsoft.AspNetCore.Mvc.TagHelpers");
            resolver.Add("MyControllers", "Microsoft.AspNetCore.Mvc");
            resolver.Add("MyApp.Models", "Microsoft.EntityFrameworkCore");
            resolver.Add("Microsoft.AspNetCore.Mvc", "Microsoft.AspNetCore.HttpAbstractions", "Microsoft.AspNetCore.Mvc.TagHelpers");
            resolver.Add("Microsoft.AspNetCore.KestrelHttpServer", "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.HttpAbstractions");
            resolver.Add("Microsoft.AspNetCore.StaticFiles", "Microsoft.AspNetCore.HttpAbstractions", "Microsoft.Extensions.Primitives");
            resolver.Add("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.HttpAbstractions");
            resolver.Add("Microsoft.AspNetCore.HttpAbstractions", "Microsoft.Extensions.Primitives");

            // Act
            var assemblies = resolver.ResolveAssemblies();

            // Assert
            Assert.Equal(new[] { "MyControllers", "MyTagHelpers" }, assemblies.OrderBy(a => a));
        }

        [Fact]
        public void Resolve_ReturnsItemsThatTransitivelyReferenceMvc()
        {
            // Arrange
            var resolver = new TestReferencesToMvcResolver(new[]
            {
                CreateReferenceItem("MyCMS"),
                CreateReferenceItem("MyCMS.Core"),
                CreateReferenceItem("Microsoft.AspNetCore.Mvc.ViewFeatures", isSystemReference: true),
            });

            resolver.Add("MyCMS", "MyCMS.Core");
            resolver.Add("MyCMS.Core", "Microsoft.AspNetCore.Mvc.ViewFeatures");


            // Act
            var assemblies = resolver.ResolveAssemblies();

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

        private class TestReferencesToMvcResolver : ReferenceResolver
        {
            private readonly Dictionary<string, List<AssemblyItem>> _references = new Dictionary<string, List<AssemblyItem>>();
            private readonly Dictionary<string, AssemblyItem> _lookup;

            public TestReferencesToMvcResolver(ResolveReferenceItem[] referenceItems)
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
