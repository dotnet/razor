// /* --------------------------------------------------------------------------------------------
//  * Copyright (c) Microsoft Corporation. All rights reserved.
//  * Licensed under the MIT License. See License.txt in the project root for license information.
//  * ------------------------------------------------------------------------------------------ */

// This file is used at the command line to download VSCode insiders and run all of our functional tests.

import * as cp from 'child_process';
import * as path from 'path';
import { downloadAndUnzipVSCode, resolveCliPathFromVSCodeExecutablePath, runTests } from 'vscode-test';

async function main() {
    try {
        const extensionDevelopmentPath = path.resolve(__dirname, '../../../src/Microsoft.AspNetCore.Razor.VSCode.Extension/');
        const extensionTestsPath = path.resolve(__dirname, './index');
        const testAppFolder = path.resolve(__dirname, '../../testapps');

        const vscodeExecutablePath = await downloadAndUnzipVSCode('insiders');
        const cliPath = resolveCliPathFromVSCodeExecutablePath(vscodeExecutablePath);

        cp.spawnSync(cliPath, ['--install-extension', 'ms-vscode.csharp'], {
            encoding: 'utf-8',
            stdio: 'inherit',
        });

        // Download VS Code, unzip it and run the integration test
        await runTests({
            vscodeExecutablePath,
            extensionDevelopmentPath,
            extensionTestsPath,
            launchArgs: [ testAppFolder ],
        });
    } catch (err) {
        console.error('Failed to run functional tests');
        process.exit(1);
    }
}

// tslint:disable-next-line: no-floating-promises
main();
