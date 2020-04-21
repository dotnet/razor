/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * -------------------------------------------------------------------------------------------- */

import * as vscode from 'vscode';
import * as vscodeapi from 'vscode';

export class ProposedApisFeature {
    // eslint-disable-next-line @typescript-eslint/require-await
    public async register(vscodeType: typeof vscodeapi, _localRegistrations: vscode.Disposable[]): Promise<void> {
        if (vscodeType.env.appName.endsWith('Insiders')) {
            return;
        }
    }
}
