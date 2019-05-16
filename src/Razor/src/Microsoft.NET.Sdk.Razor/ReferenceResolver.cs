// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    /// <summary>
    /// Resolves assemblies that reference one of the specified "targetAssemblies" either directly or transitively.
    /// </summary>
    public class ReferenceResolver
    {
        private readonly HashSet<string> _mvcAssemblies;
        private readonly Dictionary<string, AssemblyItem> _lookup = new Dictionary<string, AssemblyItem>(StringComparer.Ordinal);

        public ReferenceResolver(IReadOnlyList<string> targetAssemblies, IReadOnlyList<ResolveReferenceItem> resolveReferenceResult)
        {
            _mvcAssemblies = new HashSet<string>(targetAssemblies, StringComparer.Ordinal);

            foreach (var item in resolveReferenceResult)
            {
                var assemblyItem = new AssemblyItem(item);
                _lookup[item.AssemblyName] = assemblyItem;
            }
        }

        public IReadOnlyList<string> ResolveAssemblies()
        {
            var applicationParts = new List<string>();
            foreach (var item in _lookup)
            {
                var classification = Resolve(item.Value);
                if (classification == DependencyClassification.ReferencesMvc)
                {
                    applicationParts.Add(item.Key);
                }

                // It's not interesting for us to know if a dependency has a classification of MvcReference.
                // All applications targeting the Microsoft.AspNetCore.App will have have a reference to Mvc.
            }

            return applicationParts;
        }

        private DependencyClassification Resolve(AssemblyItem assemblyItem)
        {
            if (assemblyItem.DependencyClassification != DependencyClassification.Unknown)
            {
                return assemblyItem.DependencyClassification;
            }

            if (assemblyItem.ResolveReferenceItem == null)
            {
                // We encountered a dependency that isn't part of this assembly's dependency set. We'll see if it happens to be an MVC assembly.
                // This might be useful in scenarios where the app does not have a framework reference at the entry point,
                // but the transitive dependency does.
                assemblyItem.DependencyClassification = _mvcAssemblies.Contains(assemblyItem.Name) ?
                    DependencyClassification.MvcReference :
                    DependencyClassification.DoesNotReferenceMvc;

                return assemblyItem.DependencyClassification;
            }

            if (assemblyItem.ResolveReferenceItem.IsSystemReference)
            {
                // We do not allow transitive references to MVC via a framework reference to count.
                // e.g. depending on Microsoft.AspNetCore.SomeThingNewThatDependsOnMvc would not result in an assembly being treated as
                // referencing MVC.
                assemblyItem.DependencyClassification = _mvcAssemblies.Contains(assemblyItem.Name) ?
                    DependencyClassification.MvcReference :
                    DependencyClassification.DoesNotReferenceMvc;

                return assemblyItem.DependencyClassification;
            }

            if (_mvcAssemblies.Contains(assemblyItem.Name))
            {
                assemblyItem.DependencyClassification = DependencyClassification.MvcReference;
                return assemblyItem.DependencyClassification;
            }

            var dependencyClassification = DependencyClassification.DoesNotReferenceMvc;
            foreach (var referenceItem in GetReferences(assemblyItem.ResolveReferenceItem.Path))
            {
                var classification = Resolve(referenceItem);
                if (classification == DependencyClassification.MvcReference || classification == DependencyClassification.ReferencesMvc)
                {
                    dependencyClassification = DependencyClassification.ReferencesMvc;
                    break;
                }
            }

            assemblyItem.DependencyClassification = dependencyClassification;
            return dependencyClassification;
        }

        protected virtual IReadOnlyList<AssemblyItem> GetReferences(string file)
        {
            try
            {
                using var peReader = new PEReader(File.OpenRead(file));
                if (!peReader.HasMetadata)
                {
                    return Array.Empty<AssemblyItem>(); // not a managed assembly
                }

                var metadataReader = peReader.GetMetadataReader();

                var assemblyItems = new List<AssemblyItem>();
                foreach (var handle in metadataReader.AssemblyReferences)
                {
                    var reference = metadataReader.GetAssemblyReference(handle);
                    var referenceName = metadataReader.GetString(reference.Name);

                    if (_lookup.TryGetValue(referenceName, out var assemblyItem))
                    {
                        assemblyItems.Add(assemblyItem);
                    }
                    else
                    {
                        // A dependency references an item that isn't referenced by this project.
                        // We'll construct an item for so that we can calculate the classification based on it's name.
                        assemblyItems.Add(new AssemblyItem(referenceName));
                    }
                }

                return assemblyItems;
            }
            catch (BadImageFormatException)
            {
                // not a PE file, or invalid metadata
            }

            return Array.Empty<AssemblyItem>(); // not a managed assembly
        }

        protected enum DependencyClassification
        {
            Unknown,
            DoesNotReferenceMvc,
            ReferencesMvc,
            MvcReference,
        }

        protected class AssemblyItem
        {
            public AssemblyItem(ResolveReferenceItem resolveReferenceItem)
                : this(resolveReferenceItem.AssemblyName)
            {
                ResolveReferenceItem = resolveReferenceItem;
            }

            public AssemblyItem(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public ResolveReferenceItem ResolveReferenceItem { get; }

            public DependencyClassification DependencyClassification { get; set; }
        }
    }
}
