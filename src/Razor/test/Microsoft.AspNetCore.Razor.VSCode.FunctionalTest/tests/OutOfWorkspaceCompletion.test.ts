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
    testAppsRoot,
    waitForProjectReady,
} from './TestUtil';

suite('Completions', () => {
    before(async () => {
        await waitForProjectReady(mvcWithComponentsRoot);
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

    test('Completions out of Workspace work', async () => {
        const outOfWorkspace = path.join(testAppsRoot, '..', 'OutOfWorkspaceFile.cshtml');
        const outOfWorkspaceDoc = await vscode.workspace.openTextDocument(outOfWorkspace);
        const outOfWorkspaceEditor = await vscode.window.showTextDocument(outOfWorkspaceDoc);
        const firstLine = new vscode.Position(0, 0);
        await outOfWorkspaceEditor.edit(edit => edit.insert(firstLine, '<a'));
        const completions = await vscode.commands.executeCommand<vscode.CompletionList>(
            'vscode.executeCompletionItemProvider',
            outOfWorkspaceDoc.uri,
            new vscode.Position(0, 2));

        const hasCompletion = (text: string) => completions!.items.some(item => item.insertText === text);

        assert.ok(hasCompletion('a'), 'Should have completion for "a"');
    });
});
