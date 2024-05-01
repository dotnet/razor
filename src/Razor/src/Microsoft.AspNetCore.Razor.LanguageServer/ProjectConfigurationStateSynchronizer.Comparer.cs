// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal partial class ProjectConfigurationStateSynchronizer
{
    private sealed class Comparer : IEqualityComparer<Work>
    {
        public static readonly Comparer Instance = new();

        private Comparer()
        {
        }

        public bool Equals(Work? x, Work? y)
        {
            if (x is null)
            {
                return y is null;
            }
            else if (y is null)
            {
                return x is null;
            }

            // For purposes of removing duplicates from batches, two Work instances
            // are equal only if their identifying properties are equal. So, only
            // configuration file paths and project keys.

            if (x.ProjectKey != y.ProjectKey)
            {
                return false;
            }

            // If we're skipping an item, then it's never equal to an item that isn't being skipped so a more recent skipped item
            // won't cause a previous item to be de-duped when it should be processed.
            if (x.Skip ^ y.Skip)
            {
                return false;
            }

            return (x, y) switch
            {
                (AddProject, AddProject) => true,

                (ResetProject { ProjectKey: var keyX },
                 ResetProject { ProjectKey: var keyY })
                    => keyX == keyY,

                (UpdateProject { ProjectKey: var keyX },
                 UpdateProject { ProjectKey: var keyY })
                    => keyX == keyY,

                _ => false,
            };
        }

        public int GetHashCode(Work obj)
        {
            var hash = HashCodeCombiner.Start();
            hash.Add(obj.Skip);
            hash.Add(obj.ProjectKey.GetHashCode());
            hash.Add(obj.GetType().GetHashCode());
            return hash.CombinedHash;
        }
    }
}
