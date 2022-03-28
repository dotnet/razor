// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.OperationProgress;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(RazorSolutionStatusService))]
    internal class VisualStudioWindowsSolutionStatusService : RazorSolutionStatusService
    {
        private readonly IVsOperationProgressStatusService? _operationProgressStatusService;

        [ImportingConstructor]
        public VisualStudioWindowsSolutionStatusService([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider!!)
        {
            if (serviceProvider.GetService(typeof(SVsOperationProgress)) is IVsOperationProgressStatusService service)
            {
                _operationProgressStatusService = service;
            }
        }

        public override bool TryGetIntelliSenseStatus([NotNullWhen(returnValue: true)] out RazorSolutionStatus? status)
        {
            if (_operationProgressStatusService == null)
            {
                status = null;
                return false;
            }

            var shellStatus = _operationProgressStatusService.GetStageStatusForSolutionLoad(CommonOperationProgressStageIds.Intellisense);
            if (shellStatus == null)
            {
                status = null;
                return false;
            }

            status = new VisualStudioWindowsSolutionStatus(shellStatus);
            return true;
        }

        private class VisualStudioWindowsSolutionStatus : RazorSolutionStatus
        {
            private readonly IVsOperationProgressStageStatusForSolutionLoad _shellStatus;

            public VisualStudioWindowsSolutionStatus(IVsOperationProgressStageStatusForSolutionLoad shellStatus)
            {
                _shellStatus = shellStatus;
            }

            public override bool IsAvailable => !_shellStatus.IsInProgress;

            public override event PropertyChangedEventHandler PropertyChanged
            {
                add
                {
                    _shellStatus.PropertyChanged += value;
                }
                remove
                {
                    _shellStatus.PropertyChanged -= value;
                }
            }
        }
    }
}
