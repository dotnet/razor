/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as assert from 'assert';
import { afterEach, before, beforeEach } from 'mocha';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    mvcWithComponentsRoot,
    pollUntil,
    waitForProjectReady,
} from './TestUtil';

let razorPath: string;
let razorDoc: vscode.TextDocument;
let razorEditor: vscode.TextEditor;

suite('Code Actions', () => {
    before(async () => {
        await waitForProjectReady(mvcWithComponentsRoot);
    });

    beforeEach(async () => {
        razorPath = path.join(mvcWithComponentsRoot, 'Views', 'Shared', 'NavMenu.razor');
        razorDoc = await vscode.workspace.openTextDocument(razorPath);
        razorEditor = await vscode.window.showTextDocument(razorDoc);
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

    test('Can provide Code Action .razor file', async () => {
        const firstLine = new vscode.Position(0, 0);
        await razorEditor.edit(edit => edit.insert(firstLine, '@{ var x = typeof(MyClass); }\n'));

        const position = new vscode.Position(0, 21);
        const codeAction = await GetCodeAction(razorDoc.uri, new vscode.Range(position, position));

        assert.equal(codeAction.length, 1);
        assert.equal(codeAction[0].title, 'Using');
    });

    async function GetCodeAction(fileUri: vscode.Uri, position: vscode.Range): Promise<vscode.CodeAction[]> {
        return await vscode.commands.executeCommand('vscode.executeCodeActionProvider', fileUri, position) as vscode.CodeAction[];
    }
});
