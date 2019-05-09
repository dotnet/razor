// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language
{
    internal class DefaultBoundAttributeParameterDescriptorBuilder : BoundAttributeParameterDescriptorBuilder
    {
        private readonly DefaultBoundAttributeDescriptorBuilder _parent;
        private readonly string _kind;
        private readonly Dictionary<string, string> _metadata;

        private RazorDiagnosticCollection _diagnostics;

        public DefaultBoundAttributeParameterDescriptorBuilder(DefaultBoundAttributeDescriptorBuilder parent, string kind)
        {
            _parent = parent;
            _kind = kind;

            _metadata = new Dictionary<string, string>();
        }

        public override string Name { get; set; }

        public override string TypeName { get; set; }

        public override bool IsEnum { get; set; }

        public override string Documentation { get; set; }

        public override string DisplayName { get; set; }

        public override IDictionary<string, string> Metadata => _metadata;

        public override RazorDiagnosticCollection Diagnostics
        {
            get
            {
                if (_diagnostics == null)
                {
                    _diagnostics = new RazorDiagnosticCollection();
                }

                return _diagnostics;
            }
        }

        public BoundAttributeParameterDescriptor Build()
        {
            var diagnostics = Array.Empty<RazorDiagnostic>();
            var descriptor = new DefaultBoundAttributeParameterDescriptor(
                _kind,
                Name,
                TypeName,
                IsEnum,
                Documentation,
                GetDisplayName(),
                new Dictionary<string, string>(Metadata),
                diagnostics);

            return descriptor;
        }

        private string GetDisplayName()
        {
            if (DisplayName != null)
            {
                return DisplayName;
            }

            return $"{_parent.DisplayName}:{Name}";
        }
    }
}
