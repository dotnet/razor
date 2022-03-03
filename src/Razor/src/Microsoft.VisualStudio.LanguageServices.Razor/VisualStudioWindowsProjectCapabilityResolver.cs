// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(ProjectCapabilityResolver))]
    internal class VisualStudioWindowsProjectCapabilityResolver : ProjectCapabilityResolver
    {
        public override bool HasCapability(string documentFilePath, object project, string capability)
        {
            if (project is not IVsHierarchy vsHierarchy)
            {
                return false;
            }

            var localHasCapability = LocalHasCapability(vsHierarchy, capability);
            return localHasCapability;
        }

        private static bool LocalHasCapability(IVsHierarchy hierarchy, string capability)
        {
            if (hierarchy is null)
            {
                return false;
            }

            try
            {
                var hasCapability = hierarchy.IsCapabilityMatch(capability);
                return hasCapability;
            }
            catch (NotSupportedException)
            {
                // IsCapabilityMatch throws a NotSupportedException if it can't create a
                // BooleanSymbolExpressionEvaluator COM object
                return false;
            }
            catch (ObjectDisposedException)
            {
                // IsCapabilityMatch throws an ObjectDisposedException if the underlying hierarchy has been disposed
                return false;
            }
        }
    }
}
