/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

import fetch from 'node-fetch';
import { finished as _finished } from 'stream';
import extract from 'extract-zip';
import { existsSync, mkdirSync, createWriteStream, readdirSync, copyFileSync } from 'fs';
import { promisify } from 'util';
import { join } from 'path';
import { tmpdir } from 'os';

var finished = promisify(_finished);

const formatLog = (text) => `[${new Date()}] ${text}`;
const log = (text) => console.log(formatLog(text));
const logError = (text) => console.error(formatLog(text));

async function downloadProxyPackage(version) {
    var nugetUrl = 'https://api.nuget.org/v3-flatcontainer';
    var packageName = 'Microsoft.AspNetCore.Components.WebAssembly.DevServer';

    const tmpDirectory = join(tmpdir(), 'blazorwasm-companion-tmp');
    if (!existsSync(tmpDirectory)) {
        mkdirSync(tmpDirectory);
    }
    const extractTarget = join(tmpDirectory, `extracted-${packageName}.${version}`);

    const versionedPackageName = `${packageName.toLowerCase()}.${version}.nupkg`;
    // nuget.org requires the package name be lower-case
    const downloadUrl = `${nugetUrl}/${packageName.toLowerCase()}/${version}/${versionedPackageName}`;
    const downloadPath = join(tmpDirectory, versionedPackageName);

    // Download and save nupkg to disk
    log(`Fetching package from ${downloadUrl}...`);
    const response = await fetch(downloadUrl);

    if (!response.ok) {
        logError(`Failed to download ${downloadUrl}`);
        return null;
    }
    const outputStream = createWriteStream(downloadPath);
    response.body.pipe(outputStream);

    // Extract nupkg to extraction directory
    log(`Extracting NuGet package with directory...`)
    await finished(outputStream);
    await extract(downloadPath, { dir: extractTarget });
    return extractTarget;
}

async function copyDebugProxyAssets(version) {
    var extracted = await downloadProxyPackage(version);
    if (!extracted) {
        return;
    }

    var srcDirectory = join(extracted, 'tools', 'BlazorDebugProxy');
    log(`Looking for installed BlazorDebugProxy in ${srcDirectory}...`);
    var targetDirectory = join(__dirname, '..', 'BlazorDebugProxy');
    log(`Using ${targetDirectory} as targetDirectory...`);
    var exists = existsSync(srcDirectory);
    if (exists) {
        log(`Copying BlazorDebugProxy assets from ${srcDirectory} to bundle...`);
        readdirSync(srcDirectory).forEach(function(file) {
            log(`Copying ${file} to target directory...`);
            copyFileSync(join(srcDirectory, file), join(targetDirectory, file));
        });
    }
}

import { debugProxyVersion } from '../package.json';
copyDebugProxyAssets(debugProxyVersion);
