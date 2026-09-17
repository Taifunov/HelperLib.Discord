$ErrorActionPreference = 'Stop'
$project = [IO.File]::ReadAllText((Join-Path $PWD 'HelperLib.Discord.csproj'))
$readme = [IO.File]::ReadAllText((Join-Path $PWD 'README.md'))
$versions = [regex]::Matches($project, '<Version>(\d+\.\d+\.\d+)</Version>')
if ($versions.Count -ne 1) { throw 'Expected exactly one stable semantic Version element.' }
$version = $versions[0].Groups[1].Value
$currentPattern = '(?m)^Current version: `' + [regex]::Escape($version) + '`\r?$'
$releasePattern = '(?m)^### ' + [regex]::Escape($version) + '\r?\n\r?\nReleased: (\d{4}-\d{2}-\d{2})\r?$'
$dates = [regex]::Matches($readme, $releasePattern)
if ([regex]::Matches($readme, $currentPattern).Count -ne 1 -or $dates.Count -ne 1) {
    throw 'Merge a prepared release PR with matching version and dated changelog before publishing.'
}
$null = [DateTime]::ParseExact($dates[0].Groups[1].Value, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
Write-Output $version
