$ErrorActionPreference = 'Stop'
$prepare = Join-Path $PSScriptRoot 'Prepare-Release.ps1'
$validate = Join-Path $PSScriptRoot 'Validate-Release.ps1'
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
    Write-Host 'Release checks passed: all bump modes, unprepared release rejection, repeated preparation rejection, missing changelog, LF.'
}
finally {
    Pop-Location
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd([IO.Path]::DirectorySeparatorChar) -eq [IO.Path]::GetTempPath().TrimEnd([IO.Path]::DirectorySeparatorChar) -and
        [IO.Path]::GetFileName($resolved).StartsWith('helperlib-release-test-')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
