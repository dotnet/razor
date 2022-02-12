/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */

const fetch = require('node-fetch');
const stream = require('stream');
const extract = require('extract-zip');
const fs = require('fs');
const util = require('util');
const path = require('path');
const os = require('os');

const finished = util.promisify(stream.finished);

const formatLog = (text) => `[${new Date()}] ${text}`;
const log = (text) => console.log(formatLog(text));
const logError = (text) => console.error(formatLog(text));

async function downloadProxyPackage(version) {
    const nugetUrl = 'https://api.nuget.org/v3-flatcontainer';
    const packageName = 'Microsoft.AspNetCore.Components.WebAssembly.DevServer';

    const tmpDirectory = path.join(os.tmpdir(), 'blazorwasm-companion-tmp');
    if (!fs.existsSync(tmpDirectory)){
        fs.mkdirSync(tmpDirectory);
    }
    const extractTarget = path.join(tmpDirectory, `extracted-${packageName}.${version}`);

    const versionedPackageName = `${packageName.toLowerCase()}.${version}.nupkg`;
    // nuget.org requires the package name be lower-case
    const downloadUrl = `${nugetUrl}/${packageName.toLowerCase()}/${version}/${versionedPackageName}`;
    const downloadPath = path.join(tmpDirectory, versionedPackageName);

    // Download and save nupkg to disk
    log(`Fetching package from ${downloadUrl}...`);
    const response = await fetch(downloadUrl);

    if (!response.ok) {
        logError(`Failed to download ${downloadUrl}`);
        throw new Error(`Unable to download BlazorDebugProxy: ${response.status} ${response.statusText}`);
    }
    const outputStream = fs.createWriteStream(downloadPath);
    response.body.pipe(outputStream);

    // Extract nupkg to extraction directory
    log(`Extracting NuGet package with directory...`)
    await finished(outputStream);
    await extract(downloadPath, { dir: extractTarget });
    return extractTarget;
}

async function copyDebugProxyAssets(version) {
    const targetDirectory = path.join(__dirname, '..', 'BlazorDebugProxy');
    const versionMarkerFile = path.join(targetDirectory, '_version');
    if (fs.existsSync(targetDirectory) && fs.existsSync(versionMarkerFile)) {
        const cachedVersion = fs.readFileSync(versionMarkerFile, { encoding: 'utf-8' });
        if (cachedVersion === version) {
            log(`Found up-to-date BlazorDebugProxy ${version}, nothing to do.`);
            return;
        }

        log(`Cached BlazorDebugProxy ${cachedVersion} is not ${version}, downloading...`);
    } else {
        log(`No existing BlazorDebugProxy version found, downloading...`);
    }

    const extracted = await downloadProxyPackage(version);

    fs.rmSync(targetDirectory, { recursive: true, force: true });

    log(`Using ${targetDirectory} as targetDirectory...`);
    log(`Cleaning ${targetDirectory}...`);
    fs.rmSync(targetDirectory, { recursive: true, force: true });
    fs.mkdirSync(targetDirectory);

    const srcDirectory = path.join(extracted, 'tools', 'BlazorDebugProxy');
    log(`Copying BlazorDebugProxy assets from ${srcDirectory} to bundle...`);
    fs.readdirSync(srcDirectory).forEach(function(file) {
        log(`Copying ${file} to target directory...`);
        fs.copyFileSync(path.join(srcDirectory, file), path.join(targetDirectory, file));
    });

    fs.writeFileSync(versionMarkerFile, version, { encoding: 'utf-8' });
}

const debugProxyVersion = require('../package.json').debugProxyVersion;
copyDebugProxyAssets(debugProxyVersion);
