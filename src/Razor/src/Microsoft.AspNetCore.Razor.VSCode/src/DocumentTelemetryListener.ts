/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import { RazorDocumentChangeKind } from './Document/RazorDocumentChangeKind';
import { RazorDocumentManager } from './Document/RazorDocumentManager';
import { TelemetryReporter } from './TelemetryReporter';

export function reportTelemetryForDocuments(
    documentManager: RazorDocumentManager,
    telemetryReporter: TelemetryReporter) {
    documentManager.onChange((event) => {
        switch (event.kind) {
            case RazorDocumentChangeKind.added:
                telemetryReporter.reportWorkspaceContainsRazor();
                break;
        }
    });
}
