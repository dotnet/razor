const fetch = require('node-fetch');
import * as stream from 'stream';
import * as extract from 'extract-zip';
import * as net from 'net';
import * as os from 'os';
import * as fs from 'fs';
import * as util from 'util';

export class LocalDebugProxyManager {
    private readonly localPath = `${os.homedir()}/.blazor-wasm-debug-proxy-local`;
    private readonly nugetUrl = 'https://api.nuget.org/v3-flatcontainer';
    private readonly packageName = 'Microsoft.AspNetCore.Components.WebAssembly.DevServer';
    private readonly finished = util.promisify(stream.finished);

    constructor() {
        if (!fs.existsSync(this.localPath)) {
            fs.mkdirSync(this.localPath);
        }
    }

    public async getDebugProxyLocalNugetPath(version: string) {
        const extractTarget = `${this.localPath}/extracted/${this.packageName}.${version}`;
        if (fs.existsSync(extractTarget)) {
            return extractTarget;
        }

        const versionedPackageName = `${this.packageName}.${version}.nupkg`;
        const downloadUrl = `${this.nugetUrl}/${this.packageName}/${version}/${versionedPackageName}`;
        const downloadPath = `${this.localPath}/${versionedPackageName}`;

        // Download and save nupkg to disk
        const response = await fetch(downloadUrl)
        const outputStream = fs.createWriteStream(downloadPath);
        response.body.pipe(outputStream);

        // Extract nupkg to extraction directory
        await this.finished(outputStream);
        await extract(downloadPath, { dir: extractTarget });
        return extractTarget;
    }

    public static getAvailablePort(initialPort: number) {
        function getNextAvailablePort(currentPort: number, cb: (port: number) => void) {
            const server = net.createServer();
            server.listen(currentPort, () => {
                server.once('close', () => {
                    cb(currentPort);
                });
                server.close();
            });
            server.on('error', () => {
                getNextAvailablePort(++currentPort, cb);
            });
        }

        return new Promise<number>(resolve => {
            getNextAvailablePort(initialPort, resolve);
        });
    }
}