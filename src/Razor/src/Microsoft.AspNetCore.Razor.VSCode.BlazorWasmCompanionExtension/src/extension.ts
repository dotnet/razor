/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { LocalDebugProxyManager } from './LocalDebugProxyManager';
import { spawn } from 'child_process';

let launchDebugProxy: vscode.Disposable;
let killDebugProxy: vscode.Disposable;

export function activate(context: vscode.ExtensionContext) {
    const outputChannel = vscode.window.createOutputChannel("Blazor WASM Debug Proxy");
    const pidsByUrl = new Map<string, number>();

    launchDebugProxy = vscode.commands.registerCommand('ms-blazorwasm-companion.launchDebugProxy', async (version = "5.0.0") => {
        try {
            outputChannel.appendLine(`Launching proxy version Blazor WASM ${version}...`);
            const localDebugProxyManager = new LocalDebugProxyManager();

            const debuggingPort = await LocalDebugProxyManager.getAvailablePort(9222);
            const debuggingHost = `http://localhost:${debuggingPort}`;

            const debugProxyLocalDirectory = await localDebugProxyManager.getDebugProxyLocalNugetPath(version);
            const debugProxyLocalPath = `${debugProxyLocalDirectory}/tools/BlazorDebugProxy/BrowserDebugHost.dll`;
            outputChannel.appendLine(`Launching debugging proxy from ${debugProxyLocalPath}`);
            const spawnedProxy = spawn('/usr/local/share/dotnet/dotnet',
                [debugProxyLocalPath , '--DevToolsUrl', debuggingHost],
                { detached: process.platform !== 'win32' });

            for await (const output of spawnedProxy.stdout) {
                outputChannel.appendLine(output);
                const matchExpr = "Now listening on: (?<url>.*)";
                const found = `${output}`.match(matchExpr);
                const url = found?.groups?.url;
                if (url) {
                    outputChannel.appendLine(`Debugging proxy is running at: ${url}`);
                    pidsByUrl.set(url, spawnedProxy.pid);
                    return {
                        url,
                        debuggingPort
                    };
                }
            }

            for await (const error of spawnedProxy.stderr) {
                outputChannel.appendLine(`ERROR: ${error}`);
            }

            return;
        } catch (error) {
            outputChannel.appendLine(error);
        }
    });

    killDebugProxy = vscode.commands.registerCommand('ms-blazorwasm-companion.killDebugProxy', (url: string) => {
        const pid = pidsByUrl.get(url);
        if (pid) {
            outputChannel.appendLine(`Terminating debug proxy server running at ${url} with PID ${pid}`);
            process.kill(pid);
        }
    });

    context.subscriptions.push(launchDebugProxy, killDebugProxy);
}

export function deactivate() {
    launchDebugProxy.dispose();
    killDebugProxy.dispose();
}
