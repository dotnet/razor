// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.CodeAnalysis.Razor.Tooltip;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    internal abstract class VSLSPTagHelperTooltipFactory : TagHelperTooltipFactoryBase
    {
        public abstract bool TryCreateTooltip(AggregateBoundElementDescription elementDescriptionInfo, out VSContainerElement tooltipContent);

        public abstract bool TryCreateTooltip(AggregateBoundAttributeDescription attributeDescriptionInfo, out VSContainerElement tooltipContent);

        public abstract bool TryCreateTooltip(AggregateBoundElementDescription elementDescriptionInfo, out VSClassifiedTextElement tooltipContent);

        public abstract bool TryCreateTooltip(AggregateBoundAttributeDescription attributeDescriptionInfo, out VSClassifiedTextElement tooltipContent);
    }
}
