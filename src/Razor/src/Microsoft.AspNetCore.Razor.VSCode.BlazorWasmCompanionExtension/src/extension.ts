/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as vscode from 'vscode';
import { spawn } from 'child_process';
import { acquireDotnetInstall } from './acquireDotnetInstall';
import { getAvailablePort } from "./getAvailablePort";

let launchDebugProxy: vscode.Disposable;
let killDebugProxy: vscode.Disposable;

export function activate(context: vscode.ExtensionContext) {
    const outputChannel = vscode.window.createOutputChannel("Blazor WASM Debug Proxy");
    const pidsByUrl = new Map<string, number>();

    launchDebugProxy = vscode.commands.registerCommand('ms-blazorwasm-companion.launchDebugProxy', async () => {
        try {
            const debuggingPort = await getAvailablePort(9222);
            const debuggingHost = `http://localhost:${debuggingPort}`;

            let dotnet = "dotnet";
            // The vscode.env.remoteName property is set when connected to
            // any kind of remote workspace. See
            // https://code.visualstudio.com/api/advanced-topics/remote-extensions#varying-behaviors-when-running-remotely-or-in-the-codespaces-browser-editor
            const isRemote = vscode.env.remoteName !== 'undefined';
            if (isRemote) {
                dotnet = await acquireDotnetInstall(outputChannel);
            }

            const debugProxyLocalPath = `${context.extensionPath}/BlazorDebugProxy/BrowserDebugHost.dll`;
            outputChannel.appendLine(`Launching debugging proxy from ${debugProxyLocalPath}`);
            const spawnedProxy = spawn(dotnet,
                [debugProxyLocalPath , '--DevToolsUrl', debuggingHost],
                { detached: process.platform !== 'win32' });

            for await (const output of spawnedProxy.stdout) {
                outputChannel.appendLine(output);
                // The debug proxy server outputs the port it is listening on in the
                // standard output of the launched application. We need to pass this URL
                // back to the debugger so we extract the URL from stdout using a regex.
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
            outputChannel.appendLine(`ERROR: ${error}`);
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
