/* ---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------- */

import { RequestType } from 'vscode-languageclient';
import { RazorLanguageServerClient } from './RazorLanguageServerClient';

export class RazorReadyHandler {
    private static readonly razorReadyEndpoint = 'razor/serverReady';
    private razorReadyHandlerType: RequestType<void, void, any, any> = new RequestType(RazorReadyHandler.razorReadyEndpoint);

    constructor(private readonly serverClient: RazorLanguageServerClient) {
    }

    public register() {
        // tslint:disable-next-line: no-floating-promises
        this.serverClient.onRequestWithParams<void, void,  any, any>(
            this.razorReadyHandlerType,
            async (request, token) => this.handleRazorReady());
    }

    private handleRazorReady(): void {
        return;
    }
}
