param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$tag = "v$Version"
$asset = "HelperLib.Discord.$Version.nupkg"
$package = Join-Path 'artifacts' $asset
if (!(Test-Path -LiteralPath $package -PathType Leaf)) { throw "Package not found: $package" }

$existing = gh release view $tag --json assets,isDraft
if ($LASTEXITCODE -eq 0) {
    $release = $existing | ConvertFrom-Json
    if ($asset -notin $release.assets.name) {
        gh release upload $tag $package
        if ($LASTEXITCODE -ne 0) { throw 'GitHub Release asset upload failed.' }
    }
    if ($release.isDraft) {
        gh release edit $tag --draft=false
        if ($LASTEXITCODE -ne 0) { throw 'GitHub Release publication failed.' }
    }
} else {
    gh release create $tag $package --verify-tag --title $tag --generate-notes
    if ($LASTEXITCODE -ne 0) { throw 'GitHub Release creation failed.' }
}
