#!/usr/bin/env bash

#
# This script is meant for testing source build by imitating some of the input parameters and conditions.
#

set -euo pipefail

scriptroot="$( cd -P "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
reporoot="$(dirname "$(dirname "$scriptroot")")"

export DotNetBuildSourceOnly='true'

 # Build repo tasks
"$reporoot/eng/common/build.sh" --restore --build --ci --configuration Release /p:ProjectToBuild=$reporoot/eng/tools/RepoTasks/RepoTasks.csproj "$@"

 # Build projects
"$reporoot/eng/common/build.sh" --restore --build --pack "$@"
