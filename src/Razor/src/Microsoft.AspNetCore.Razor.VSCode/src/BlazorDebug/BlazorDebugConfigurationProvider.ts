﻿/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */
import { readFileSync } from 'fs';
import { join } from 'path';
import { fileURLToPath } from 'url';
import * as vscode from 'vscode';

import { RazorLogger } from '../RazorLogger';
import { JS_DEBUG_NAME, SERVER_APP_NAME } from './Constants';
import { onDidTerminateDebugSession } from './TerminateDebugHandler';

export class BlazorDebugConfigurationProvider implements vscode.DebugConfigurationProvider {

    constructor(private readonly logger: RazorLogger, private readonly vscodeType: typeof vscode) { }

    public async resolveDebugConfiguration(folder: vscode.WorkspaceFolder | undefined, configuration: vscode.DebugConfiguration): Promise<vscode.DebugConfiguration | undefined> {
        /**
         * The Blazor WebAssembly app should only be launched if the
         * launch configuration is a launch request. Attach requests will
         * only launch the browser.
         */
        if (configuration.request === 'launch') {
            await this.launchApp(folder, configuration);
        }

        let inspectUri = '{wsProtocol}://{url.hostname}:{url.port}/_framework/debug/ws-proxy?browser={browserInspectUri}';
        let url = 'https://localhost:5001';
        try {
            if (folder !== undefined) {
                let folderPath = configuration.cwd ? configuration.cwd : fileURLToPath(folder.uri.toString());
                folderPath = folderPath.replace('${workspaceFolder}', fileURLToPath(folder.uri.toString()));
                const launchSettings = JSON.parse(readFileSync(join(folderPath, 'Properties', 'launchSettings.json'), 'utf8'));
                if (launchSettings?.profiles && launchSettings?.profiles[Object.keys(launchSettings.profiles)[0]]?.inspectUri) {
                    inspectUri = launchSettings.profiles[Object.keys(launchSettings.profiles)[0]].inspectUri;
                    url = launchSettings.profiles[Object.keys(launchSettings.profiles)[0]].applicationUrl.split(';', 1)[0];
                }
            }
        } catch (error: any) {
            this.logger.logError('[DEBUGGER] Error while getting information from launchSettings.json: ', error as Error);
        }

        await this.launchBrowser(
            folder,
            configuration,
            inspectUri,
            url);

        /**
         * If `resolveDebugConfiguration` returns undefined, then the debugger
         * launch is canceled. Here, we opt to manually launch the browser
         * configuration using `startDebugging` above instead of returning
         * the configuration to avoid a bug where VS Code is unable to resolve
         * the debug adapter for the browser debugger.
         */
        return undefined;
    }

    private async launchApp(folder: vscode.WorkspaceFolder | undefined, configuration: vscode.DebugConfiguration) {
        const program = configuration.hosted ? configuration.program : 'dotnet';
        const cwd = configuration.cwd || '${workspaceFolder}';
        const args = configuration.hosted ? [] : ['run'];

        const app = {
            name: SERVER_APP_NAME,
            type: 'coreclr',
            request: 'launch',
            prelaunchTask: 'build',
            program,
            args,
            cwd,
            env: {
                ASPNETCORE_ENVIRONMENT: 'Development',
                ...configuration.env,
            },
            launchBrowser: {
                enabled: false,
            },
            ...configuration.dotNetConfig,
        };

        try {
            await this.vscodeType.debug.startDebugging(folder, app);
            if (process.platform !== 'win32') {
                const terminate = this.vscodeType.debug.onDidTerminateDebugSession(async event => {
                    const blazorDevServer = 'blazor-devserver\\.dll';
                    const dir = folder && folder.uri && folder.uri.fsPath;
                    const regexEscapedDir = dir!.toLowerCase()!.replace(/\//g, '\\/');
                    const launchedApp = configuration.hosted ? app.program : `${regexEscapedDir}.*${blazorDevServer}|${blazorDevServer}.*${regexEscapedDir}`;
                    await onDidTerminateDebugSession(event, this.logger, launchedApp);
                    terminate.dispose();
                });
            }
        } catch (error) {
            this.logger.logError('[DEBUGGER] Error when launching application: ', error as Error);
        }
    }

    private async launchBrowser(folder: vscode.WorkspaceFolder | undefined, configuration: vscode.DebugConfiguration, inspectUri: string, url: string) {
        const browser = {
            name: JS_DEBUG_NAME,
            type: configuration.browser === 'edge' ? 'pwa-msedge' : 'pwa-chrome',
            request: 'launch',
            timeout: configuration.timeout || 30000,
            url: configuration.url || url,
            webRoot: configuration.webRoot || '${workspaceFolder}',
            inspectUri,
            trace: configuration.trace || false,
            noDebug: configuration.noDebug || false,
            ...configuration.browserConfig,
            // When the browser debugging session is stopped, propogate
            // this and terminate the debugging session of the Blazor dev server.
            cascadeTerminateToConfigurations: [SERVER_APP_NAME],
        };

        try {
            /**
             * The browser debugger will immediately launch after the
             * application process is started. It waits a `timeout`
             * interval before crashing after being unable to find the launched
             * process.
             *
             * We do this to provide immediate visual feedback to the user
             * that their debugger session has started.
             */
            await this.vscodeType.debug.startDebugging(folder, browser);
        } catch (error) {
            this.logger.logError(
                '[DEBUGGER] Error when launching browser debugger: ',
                error as Error,
            );
            const message = `There was an unexpected error while launching your debugging session. Check the console for helpful logs and visit the debugging docs for more info.`;
            this.vscodeType.window.showErrorMessage(message, `View Debug Docs`, `Ignore`).then(async result => {
                if (result === 'View Debug Docs') {
                    const debugDocsUri = 'https://aka.ms/blazorwasmcodedebug';
                    await this.vscodeType.commands.executeCommand(`vcode.open`, debugDocsUri);
                }
            });
        }
    }
}
