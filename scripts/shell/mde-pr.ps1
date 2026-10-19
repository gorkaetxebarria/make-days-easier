# mde-pr.ps1 - PowerShell wrapper for `mde pr`.
#
# Usage: dot-source this file from your $PROFILE, then run:
#   mde-pr <pull-request-id> [-Ide code|vs]
#
# Runs `mde pr <id> --output json`, parses ONLY the documented stdout contract
# {"schemaVersion":1,"repositoryPath":"<absolute-path>"} and changes the current
# location after success. Diagnostics from mde go to stderr and are shown as-is.
# On any failure the location is left unchanged.

function mde-pr {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateRange(1, [int]::MaxValue)]
        [int] $Id,

        [ValidateSet('code', 'vs')]
        [string] $Ide = 'code'
    )

    $stdout = & mde pr $Id --ide $Ide --output json
    if ($LASTEXITCODE -ne 0) {
        $global:LASTEXITCODE = $LASTEXITCODE
        return
    }

    try {
        $result = ($stdout -join "`n") | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        Write-Error "mde-pr: unexpected output from 'mde pr'; location not changed."
        return
    }

    $path = $result.repositoryPath
    if ($result.schemaVersion -ne 1 -or [string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path -PathType Container)) {
        Write-Error "mde-pr: unexpected output from 'mde pr'; location not changed."
        return
    }

    Set-Location -LiteralPath $path
}
