/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as fs from 'fs';
import * as glob from 'glob';
import * as Mocha from 'mocha';
import * as os from 'os';
import * as path from 'path';
import * as vscode from 'vscode';

// This file controls which tests are run during the functional test process.

function getInstalledExtensions() {
    const extensions: Array<vscode.Extension<any>> = vscode.extensions.all
        .filter(extension => extension.packageJSON.isBuiltin === false);

    return extensions.sort((a, b) =>
        a.packageJSON.name.toLowerCase().localeCompare(b.packageJSON.name.toLowerCase()));
}

function generateExtensionTable() {
    const extensions = getInstalledExtensions();
    if (extensions.length <= 0) {
        return 'none';
    }

    const tableHeader = `|Extension|Author|Version|${os.EOL}|---|---|---|`;
    const table = extensions.map(
        (e) => `|${e.packageJSON.name}|${e.packageJSON.publisher}|${e.packageJSON.version}|`).join(os.EOL);

    const extensionTable = `
${tableHeader}${os.EOL}${table};
`;

    return extensionTable;
}

export async function run(): Promise<void> {
    const mocha = new Mocha({
        ui: 'tdd',
        timeout: 349000,
    });
    mocha.useColors(true);

    const testsRoot = path.resolve(__dirname, '..');

    const razorConfiguration = vscode.workspace.getConfiguration('razor');
    const devmode = razorConfiguration.get('devmode');

    const extensionTable = generateExtensionTable();
    console.log('Installed Extensions:');
    console.log(extensionTable);

    if (!devmode) {
        console.log('Dev mode detected as disabled, configuring Razor Dev Mode');
        await vscode.commands.executeCommand('extension.configureRazorDevMode');
    }

    let testFilter: string | undefined;
    if (process.env.runSingleTest === 'true') {
        testFilter = await vscode.window.showInputBox({
                prompt: 'Test file filter',
                placeHolder: '**.test.js',
            });
    }

    if (!testFilter) {
        testFilter = 'Super.test.js';
    } else if (!testFilter.endsWith('.test.js')) {
        testFilter += '**.test.js';
    }

    return new Promise((c, e) => {
        glob(`**/${testFilter}`, { cwd: testsRoot }, (err, files) => {
            if (err) {
                return e(err);
            }
            const testArtifacts = path.join(testsRoot, '..', '..', '..', '..', 'artifacts', 'TestResults');
            ensureDirectory(testArtifacts);
            const testResults = path.join(testArtifacts, 'Debug');
            const resolvedTestResults = path.resolve(testResults);
            ensureDirectory(resolvedTestResults);
            const file = path.join(resolvedTestResults, 'VSCode-FunctionalTests.xml');

            mocha.reporter('xunit', {output: file});

            // Add files to the test suite
            files.forEach(f => mocha.addFile(path.resolve(testsRoot, f)));

            try {
                // Run the mocha test
                mocha.run(failures => {
                    if (failures > 0) {
                        e(new Error(`${failures} tests failed.`));
                    } else {
                        c();
                    }
                });
            } catch (err) {
                e(err);
            }
        });
    });
}

function ensureDirectory(directory: string) {
    if (!fs.existsSync(directory)) {
        fs.mkdirSync(directory);
    }
}
