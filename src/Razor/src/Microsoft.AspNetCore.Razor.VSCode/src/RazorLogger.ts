/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import * as fs from 'fs';
import * as path from 'path';
import { IEventEmitterFactory } from './IEventEmitterFactory';
import { Trace } from './Trace';
import * as vscode from './vscodeAdapter';

export class RazorLogger implements vscode.Disposable {
    public static readonly logName = 'Razor Log';
    public verboseEnabled!: boolean;
    public messageEnabled!: boolean;
    public readonly outputChannel: vscode.OutputChannel;

    private readonly onLogEmitter: vscode.EventEmitter<string>;
    private readonly onTraceLevelChangeEmitter: vscode.EventEmitter<Trace>;

    constructor(
        private readonly vscodeApi: vscode.api,
        eventEmitterFactory: IEventEmitterFactory,
        public trace: Trace) {
        this.processTraceLevel();
        this.onLogEmitter = eventEmitterFactory.create<string>();
        this.onTraceLevelChangeEmitter = eventEmitterFactory.create<Trace>();

        this.outputChannel = this.vscodeApi.window.createOutputChannel(RazorLogger.logName);

        this.logRazorInformation();
    }

    public setTraceLevel(trace: Trace) {
        this.trace = trace;
        this.processTraceLevel();
        this.logMessage(`Updated trace level to: ${Trace[this.trace]}`);
        this.onTraceLevelChangeEmitter.fire(this.trace);
    }

    public get onLog() { return this.onLogEmitter.event; }

    public get onTraceLevelChange() { return this.onTraceLevelChangeEmitter.event; }

    public logAlways(message: string) {
        this.logWithMarker(message);
    }

    public logWarning(message: string) {
        // Always log warnings
        const warningPrefixedMessage = `(Warning) ${message}`;
        this.logAlways(warningPrefixedMessage);
    }

    public logError(message: string, error: unknown) {
        if (error instanceof Error) {
            this.logErrorInternal(message, error);
        } else {
            const errorMsg = String(error);
            this.logErrorInternal(message, Error(errorMsg));
        }
    }

    public logMessage(message: string) {
        if (this.messageEnabled) {
            this.logWithMarker(message);
        }
    }

    public logVerbose(message: string) {
        if (this.verboseEnabled) {
            this.logWithMarker(message);
        }
    }

    public dispose() {
        this.outputChannel.dispose();
    }

    private logErrorInternal(message: string, error: Error) {
        // Always log errors
        const errorPrefixedMessage = `(Error) ${message}
${error.message}
Stack Trace:
${error.stack}`;
        this.logAlways(errorPrefixedMessage);
    }

    private logWithMarker(message: string) {
        const timeString = new Date().toLocaleTimeString();
        const markedMessage = `[Client - ${timeString}] ${message}`;

        this.log(markedMessage);
    }

    private log(message: string) {
        this.outputChannel.appendLine(message);

        this.onLogEmitter.fire(message);
    }

    private logRazorInformation() {
        const packageJsonContents = readOwnPackageJson();

        this.log(
            '--------------------------------------------------------------------------------');
        this.log(`Razor.VSCode version ${packageJsonContents.defaults.razor}`);
        this.log(
            '--------------------------------------------------------------------------------');
        this.log(`Razor's trace level is currently set to '${Trace[this.trace]}'`);
        this.log(
            ' - To change Razor\'s trace level set \'razor.trace\' to ' +
            '\'Off\', \'Messages\' or \'Verbose\' and then restart VSCode.');
        this.log(
            ' - To report issues invoke the \'Report a Razor issue\' command via the command palette.');
        this.log(
            '-----------------------------------------------------------------------' +
            '------------------------------------------------------');
        this.log('');
    }

    private processTraceLevel() {
        this.verboseEnabled = this.trace >= Trace.Verbose;
        this.messageEnabled = this.trace >= Trace.Messages;
    }
}

function readOwnPackageJson() {
    const packageJsonPath = findInDirectoryOrAncestor(__dirname, 'package.json');
    return JSON.parse(fs.readFileSync(packageJsonPath).toString());
}

function findInDirectoryOrAncestor(dir: string, filename: string) {
    while (true) {
        const candidate = path.join(dir, filename);
        if (fs.existsSync(candidate)) {
            return candidate;
        }

        const parentDir = path.dirname(dir);
        if (parentDir === dir) {
            throw new Error(`Could not find '${filename}' in or above '${dir}'.`);
        }

        dir = parentDir;
    }
}
