$ErrorActionPreference = 'Stop'
$prepare = Join-Path $PSScriptRoot 'Prepare-Release.ps1'
$validate = Join-Path $PSScriptRoot 'Validate-Release.ps1'
$publish = Join-Path $PSScriptRoot 'Publish-GitHubRelease.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('helperlib-release-test-' + [guid]::NewGuid())
$null = New-Item -ItemType Directory -Path $fixture
Push-Location $fixture
try {
    foreach ($case in @(
        @{ Bump = 'current'; Version = '2.3.4' },
        @{ Bump = 'patch'; Version = '2.3.5' },
        @{ Bump = 'minor'; Version = '2.4.0' },
        @{ Bump = 'major'; Version = '3.0.0' }
    )) {
        [IO.File]::WriteAllText((Join-Path $fixture 'HelperLib.Discord.csproj'), '<Project><PropertyGroup><Version>2.3.4</Version></PropertyGroup></Project>')
        $readme = 'Current version: `2.3.4`' + "`n`n### $($case.Version)`n`nReleased: in progress`n"
        [IO.File]::WriteAllText((Join-Path $fixture 'README.md'), $readme)
        $rejected = $false
        try { $null = & $validate } catch { $rejected = $true }
        if (!$rejected) { throw 'An in-progress release must not publish.' }
        if ((& $prepare -Bump $case.Bump) -ne $case.Version) { throw 'Unexpected version bump.' }
        if ((& $validate) -ne $case.Version) { throw 'Prepared version did not validate.' }
        if ([IO.File]::ReadAllText((Join-Path $fixture 'README.md')).Contains("`r")) { throw 'Release introduced CRLF.' }
        $rejected = $false
        try { $null = & $prepare -Bump current } catch { $rejected = $true }
        if (!$rejected) { throw 'Already-finalized changelog must not be prepared twice.' }
    }

    $before = [IO.File]::ReadAllText((Join-Path $fixture 'HelperLib.Discord.csproj'))
    $rejected = $false
    try { $null = & $prepare -Bump minor } catch { $rejected = $true }
    if (!$rejected -or [IO.File]::ReadAllText((Join-Path $fixture 'HelperLib.Discord.csproj')) -ne $before) {
        throw 'Missing changelog must fail without changing the project.'
    }
    $null = New-Item -ItemType Directory -Path artifacts
    [IO.File]::WriteAllText((Join-Path $fixture 'artifacts/HelperLib.Discord.1.0.0.nupkg'), 'test package')
    # Shadow the CLI so these checks never contact GitHub or publish anything.
    function gh {
        $calls.Add(($args -join ' '))
        $global:LASTEXITCODE = 0
        if ($args[1] -eq 'view') {
            $global:LASTEXITCODE = $case.ViewExit
            $case.Json
        } elseif ($case.FailWrite) { $global:LASTEXITCODE = 1 }
    }
    foreach ($case in @(
        @{ ViewExit = 1; Json = ''; Expected = 'create' },
        @{ ViewExit = 0; Json = '{"assets":[],"isDraft":false}'; Expected = 'upload' },
        @{ ViewExit = 0; Json = '{"assets":[{"name":"HelperLib.Discord.1.0.0.nupkg"}],"isDraft":false}'; Expected = 'none' },
        @{ ViewExit = 0; Json = '{"assets":[],"isDraft":true}'; Expected = 'edit' },
        @{ ViewExit = 1; Json = ''; Expected = 'create'; FailWrite = $true },
        @{ ViewExit = 0; Json = '{"assets":[],"isDraft":true}'; Expected = 'upload'; FailWrite = $true }
    )) {
        $calls = [Collections.Generic.List[string]]::new()
        $failed = $false
        try { & $publish -Version '1.0.0' } catch { $failed = $true }
        if ($failed -ne [bool]$case.FailWrite) { throw 'GitHub Release failure was not handled correctly.' }
        if ($case.Expected -eq 'none') {
            if ($calls.Count -ne 1) { throw 'Existing release asset must not be overwritten.' }
        } elseif ($calls[-1] -notlike "release $($case.Expected) v1.0.0*") {
            throw "Unexpected GitHub Release command: $($calls[-1])"
        }
        if ($case.Expected -eq 'create' -and $calls[-1] -notlike '*--verify-tag --title v1.0.0 --generate-notes') {
            throw 'Release must use the existing tag and generate notes.'
        }
    }
    Write-Host 'Release checks passed: version preparation, validation, LF, GitHub Release creation, retries and failures.'
}
finally {
    Pop-Location
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd([IO.Path]::DirectorySeparatorChar) -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -and
        [IO.Path]::GetFileName($resolved).StartsWith('helperlib-release-test-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
