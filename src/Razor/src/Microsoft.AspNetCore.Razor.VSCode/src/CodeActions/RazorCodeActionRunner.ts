/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { RazorLanguageServerClient } from '../RazorLanguageServerClient';
import { RazorLogger } from '../RazorLogger';
import { CodeActionResolutionResponse } from '../RPC/CodeActionResolutionResponse';
import { convertWorkspaceEditFromSerializable } from '../RPC/SerializableWorkspaceEdit';

export class RazorCodeActionRunner {

    constructor(
        private readonly serverClient: RazorLanguageServerClient,
        private readonly logger: RazorLogger,
    ) {}

    public register() {
        vscode.commands.registerCommand('razor/runCodeAction', (request: object) => this.runCodeAction(request), this);
    }

    private async runCodeAction(request: any): Promise<boolean | string | {}> {
        const response: CodeActionResolutionResponse = await this.serverClient.sendRequest('razor/resolveCodeAction', {Action: request.Action, Data: request.Data});
        let workspaceEdit: vscode.WorkspaceEdit;
        try {
            workspaceEdit = convertWorkspaceEditFromSerializable(response.edit);
        } catch (e) {
            this.logger.logAlways(`caught error running code action: ${e}`);
            return Promise.resolve(false);
        }
        return vscode.workspace.applyEdit(workspaceEdit);
    }
}
