/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as assert from 'assert';
import { afterEach, before, beforeEach } from 'mocha';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    pollUntil,
    simpleMvc22Root,
    waitForProjectReady,
} from './TestUtil';

let cshtmlDoc: vscode.TextDocument;
let editor: vscode.TextEditor;

suite('Hover 2.2', () => {
    before(async () => {
        await waitForProjectReady(simpleMvc22Root);
    });

    beforeEach(async () => {
        const filePath = path.join(simpleMvc22Root, 'Views', 'Home', 'Index.cshtml');
        cshtmlDoc = await vscode.workspace.openTextDocument(filePath);
        editor = await vscode.window.showTextDocument(cshtmlDoc);
    });

    afterEach(async () => {
        await vscode.commands.executeCommand('workbench.action.revertAndCloseActiveEditor');
        await pollUntil(async () => {
            await vscode.commands.executeCommand('workbench.action.closeAllEditors');
            if (vscode.window.visibleTextEditors.length === 0) {
                return true;
            }

            return false;
        }, /* timeout */ 3000, /* pollInterval */ 500, true /* suppress timeout */);
    });

    test('Can provide FullQualified CodeAction .cshtml file', async () => {
        const firstLine = new vscode.Position(0, 0);
        await editor.edit(edit => edit.insert(firstLine, '@{ var x = new HtmlString("sdf"); }\n'));

        const position = new vscode.Position(0, 21);
        const codeAction = await GetCodeAction(cshtmlDoc.uri, new vscode.Range(position, position));

        assert.equal(codeAction.length, 1);
        assert.equal(codeAction[0].title, 'Microsoft.AspNetCore.Html.HtmlString');
    });

    async function GetCodeAction(fileUri: vscode.Uri, position: vscode.Range): Promise<vscode.CodeAction[]> {
        let diagnosticsChanged = false;
        vscode.languages.onDidChangeDiagnostics(diagnosticsChangedEvent => {
            const diagnostics = vscode.languages.getDiagnostics(fileUri);
            if (diagnostics.length > 0) {
                diagnosticsChanged = true;
            }
        });

        await pollUntil(() => {
            return diagnosticsChanged;
        }, /* timeout */ 20000, /* pollInterval */ 1000, true /* suppress timeout */);

        return await vscode.commands.executeCommand('vscode.executeCodeActionProvider', fileUri, position) as vscode.CodeAction[];
    }
});
