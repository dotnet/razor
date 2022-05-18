// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Extensibility.Testing
{
    internal partial class EditorInProcess
    {
        public async Task<IEnumerable<object>> HoverAsync(int position, CancellationToken cancellationToken)
        {
            var hoverService = await GetComponentModelServiceAsync<IAsyncQuickInfoBroker>(cancellationToken);

            var view = await GetActiveTextViewAsync(cancellationToken);

            var trackingPoint = view.TextSnapshot.CreateTrackingPoint(position, PointTrackingMode.Negative);
            var quickInfoSession = await hoverService.TriggerQuickInfoAsync(view, trackingPoint, QuickInfoSessionOptions.TrackMouse, cancellationToken);
            quickInfoSession.StateChanged += QuickInfoSession_StateChanged;

            return quickInfoSession.Content;

            void QuickInfoSession_StateChanged(object sender, QuickInfoSessionStateChangedEventArgs e)
            {
                throw new NotImplementedException();
            }
        }
    }
}

