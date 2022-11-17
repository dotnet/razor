/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';

export async function activate(context: vscode.ExtensionContext) {
    const launchDebugProxy = vscode.commands.registerCommand('blazorwasm-companion.launchDebugProxy', async () => {
        return {
            inspectUri: '{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}',
        };
    });

    context.subscriptions.push(launchDebugProxy);
}
