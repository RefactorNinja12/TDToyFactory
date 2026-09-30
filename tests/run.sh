#!/bin/bash
# Same as run.ps1 (see there for options), for bash: tests/run.sh [Name] [-Slow|-All]
exec powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(dirname "$0")/run.ps1" "$@"
