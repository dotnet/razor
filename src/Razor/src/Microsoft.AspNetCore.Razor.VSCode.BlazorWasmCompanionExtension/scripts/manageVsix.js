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
const { spawn } = require("child_process");
const os = require('os');

var finished = util.promisify(stream.finished);

const log = (text) => console.log(`[${new Date()}] ${text}`)

async function downloadProxyPackage(version) {
    var nugetUrl = 'https://api.nuget.org/v3-flatcontainer';
    var packageName = 'Microsoft.AspNetCore.Components.WebAssembly.DevServer';

    const extractTarget = path.join(os.tmpdir(), 'blazorwasm-companion-tmp', 'extracted', `${packageName}.${version}`);

    const versionedPackageName = `${packageName}.${version}.nupkg`;
    const downloadUrl = `${nugetUrl}/${packageName}/${version}/${versionedPackageName}`;
    const downloadPath = path.join(os.tmpdir(), 'blazorwasm-companion-tmp', versionedPackageName);

    // Download and save nupkg to disk
    log(`Fetching package from ${downloadUrl}...`)
    const response = await fetch(downloadUrl)
    const outputStream = fs.createWriteStream(downloadPath);
    response.body.pipe(outputStream);

    // Extract nupkg to extraction directory
    log(`Extracting NuGet package with directory...`)
    await finished(outputStream);
    await extract(downloadPath, { dir: extractTarget });
    return extractTarget;
}

async function copyDebugProxyAssets(version) {
    var extracted = await downloadProxyPackage(version);
    var srcDirectory = path.join(extracted, 'tools', 'BlazorDebugProxy');
    log(`Looking for installed BlazorDebugProxy in ${srcDirectory}...`);
    var targetDirectory = path.join(__dirname, 'BlazorDebugProxy');
    log(`Using ${targetDirectory} as targetDirectory...`);
    var exists = fs.existsSync(srcDirectory);
    if (exists) {
        log(`Copying BlazorDebugProxy assets from ${srcDirectory} to bundle...`);
        fs.readdirSync(srcDirectory).forEach(function(file) {
            log(`Copying ${file} to target directory...`);
            fs.copyFileSync(path.join(srcDirectory, file), path.join(targetDirectory, file));
        });
    }
}

async function packageVsix(debugProxyVersion, outputPath) {
    await copyDebugProxyAssets(debugProxyVersion);

    const package = spawn("vsce", ["package", "-o", outputPath]);
    for await (const output of package.stdout) {
        log(output);
    }

    for await (const error of package.stderr) {
        log(error);
    }
}

async function publishVsix(debugProxyVersion, outputPath) {
    // Package the extension then publish the computed vsix
    if (!process.env.VSCODE_MARKETPLACE_TOKEN) {
        log('VSCODE_MARKETPLACE_TOKEN variable not found in environment! Aborting publish...');
    }

    await packageVsix(debugProxyVersion, outputPath);
    const package = spawn("vsce", ["publish", "--packagePath", vsixPath, "-p", process.env.VSCODE_MARKETPLACE_TOKEN]);

    for await (const output of package.stdout) {
        log(output);
    }

    for await (const error of package.stderr) {
        log(error);
    }
}

const task = process.argv[2];
const outputPath = process.argv[3];
const debugProxyVersion = require('../package.json').debugProxyVersion;

if (task === 'publish') {
    publishVsix(debugProxyVersion, outputPath);
} else {
    packageVsix(debugProxyVersion, outputPath);
}
