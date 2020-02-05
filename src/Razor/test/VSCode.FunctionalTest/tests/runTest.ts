// /* --------------------------------------------------------------------------------------------
//  * Copyright (c) Microsoft Corporation. All rights reserved.
//  * Licensed under the MIT License. See License.txt in the project root for license information.
//  * ------------------------------------------------------------------------------------------ */

// This file is used at the command line to download VSCode insiders and run all of our functional tests.

import * as cp from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { downloadAndUnzipVSCode, resolveCliPathFromVSCodeExecutablePath, runTests } from 'vscode-test';

async function main() {
    try {
        const extensionDevelopmentPath = path.resolve(__dirname, '../../../src/Microsoft.AspNetCore.Razor.VSCode.Extension/');
        console.log(`EXPERIMENTAL - Extension Development Path: ${extensionDevelopmentPath}`);
        console.log(`EXPERIMENTAL - Existence check for Extension Development Path: ${fs.existsSync(extensionDevelopmentPath)}`);

        const directoryContent = fs.readdirSync(extensionDevelopmentPath);
        console.log('EXPERIMENTAL - Directory content for development path:');
        for (const content of directoryContent) {
            console.log(`- ${content}`);
        }

        const extensionTestsPath = path.resolve(__dirname, './index.js');
        console.log(`EXPERIMENTAL - Extension Test Path: ${extensionTestsPath}`);
        console.log(`EXPERIMENTAL - Existence check for Test Path: ${fs.existsSync(extensionTestsPath)}`);
        const testAppFolder = path.resolve(__dirname, '../../testapps');
        console.log(`EXPERIMENTAL - Test App Folder: ${testAppFolder}`);
        console.log(`EXPERIMENTAL - Existence check for Test App Folder: ${fs.existsSync(testAppFolder)}`);

        const vscodeExecutablePath = await downloadAndUnzipVSCode('stable');
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
        console.error(`Failed to run functional tests. Error: ${err.message} Stack: ${err.stack}`);
        process.exit(1);
    }
}

// tslint:disable-next-line: no-floating-promises
main();
