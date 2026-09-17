param(
    [ValidateSet('current', 'patch', 'minor', 'major')]
    [string]$Bump = 'current'
)
$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PWD 'HelperLib.Discord.csproj'
$readmePath = Join-Path $PWD 'README.md'
$project = [IO.File]::ReadAllText($projectPath)
$readme = [IO.File]::ReadAllText($readmePath)
$versions = [regex]::Matches($project, '<Version>(\d+)\.(\d+)\.(\d+)</Version>')
if ($versions.Count -ne 1) { throw 'Expected exactly one stable semantic Version element.' }
$major = [int]$versions[0].Groups[1].Value
$minor = [int]$versions[0].Groups[2].Value
$patch = [int]$versions[0].Groups[3].Value
switch ($Bump) {
    major { $major++; $minor = 0; $patch = 0 }
    minor { $minor++; $patch = 0 }
    patch { $patch++ }
}
$version = "$major.$minor.$patch"
$currentPattern = '(?m)^Current version: `[^`]+`\r?$'
if ([regex]::Matches($readme, $currentPattern).Count -ne 1) {
    throw 'Expected exactly one Current version line in README.md.'
}
$releasePattern = '(?m)(^### ' + [regex]::Escape($version) + '\r?\n\r?\nReleased: )in progress\r?$'
if ([regex]::Matches($readme, $releasePattern).Count -ne 1) {
    throw "Add exactly one ### $version changelog entry with Released: in progress before preparing this release."
}
$project = [regex]::Replace($project, '<Version>\d+\.\d+\.\d+</Version>', "<Version>$version</Version>")
$readme = [regex]::Replace($readme, $currentPattern, ('Current version: `' + $version + '`'))
$releaseDate = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
$readme = [regex]::Replace($readme, $releasePattern, { param($match) $match.Groups[1].Value + $releaseDate })
[IO.File]::WriteAllText($projectPath, $project.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($readmePath, $readme.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
Write-Output $version
