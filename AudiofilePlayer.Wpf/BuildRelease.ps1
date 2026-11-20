# BuildRelease.ps1
# Builds the next version, packs it with Velopack and publishes it as a GitHub release.
# GitHub Actions runs this on every push to main (.github/workflows/release.yml).
#   .\BuildRelease.ps1             build, pack and upload
#   .\BuildRelease.ps1 -LocalOnly  build and pack into .\Releases only
#
# Requires the vpk tool, matching the Velopack package version in the csproj:
#   dotnet tool install -g vpk --version 1.3.39-g8d2844f
# and, unless -LocalOnly, a GitHub token: $env:GH_TOKEN in CI, otherwise the GitHub CLI (gh auth login).
param([switch]$LocalOnly)

Set-Location -Path $PSScriptRoot
$ErrorActionPreference = 'Stop'

$AppName = "AudiofilePlayer"      # the Velopack pack id; it was "Mp3Player" up to version 1.1.8
$AppTitle = "Audiofile Player"    # the name on shortcuts and in Installed apps
$ExeName = "AudiofilePlayer.exe"
$RepoUrl = "https://github.com/kreaton/AudiofilePlayer"
$FirstVersion = "1.1.0"
$PublishDir = ".\publish"
$ReleasesDir = ".\Releases"

if (-not $LocalOnly) {
    # Fetch the latest published release so the version continues from it and vpk can build a delta
    $token = if ($env:GH_TOKEN) { $env:GH_TOKEN } else { gh auth token }
    if (-not $token) { throw "No GitHub token (set GH_TOKEN or run: gh auth login)" }
    vpk download github --repoUrl $RepoUrl --token $token -o $ReleasesDir
    if ($LASTEXITCODE -ne 0) { throw "vpk download failed" }
}

# Find the highest version already packed. It is read from the full package's file name because
# vpk download only fetches the package: a clean Releases folder has no releases.win.json.
# Any pack id counts, so the numbering carries on across the rename from Mp3Player.
$largestVersion = Get-ChildItem -Path $ReleasesDir -Filter "*-full.nupkg" -ErrorAction SilentlyContinue
    | ForEach-Object { if ($_.Name -match '-(\d+\.\d+\.\d+)-full\.nupkg$') { [version]$Matches[1] } }
    | Sort-Object -Descending
    | Select-Object -First 1

# Packages under the old pack id can't be the base of a delta for this one, so leave them out of the pack.
Get-ChildItem -Path $ReleasesDir -Filter "*.nupkg" -ErrorAction SilentlyContinue
    | Where-Object { -not $_.Name.StartsWith("$AppName-") }
    | Remove-Item
if ($largestVersion) {
    # Increment the patch number by 1
    $newVersion = "{0}.{1}.{2}" -f $largestVersion.Major, $largestVersion.Minor, ($largestVersion.Build + 1)
}
else {
    $newVersion = $FirstVersion
}

Write-Host "Publishing $AppName version $newVersion (framework-dependent, win-x64)"
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
dotnet publish -c Release -r win-x64 --self-contained false -p:Version=$newVersion -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Packing $AppName version $newVersion with Velopack"
# --framework makes Setup.exe install the .NET 10 Desktop Runtime if it is missing
vpk pack -u $AppName --packTitle $AppTitle -v $newVersion -p $PublishDir -e $ExeName -o $ReleasesDir --icon .\app.ico --runtime win-x64 --framework net10.0-x64-desktop
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

if (-not $LocalOnly) {
    Write-Host "Uploading $AppName version $newVersion to GitHub"
    # In CI, tag the commit that was built rather than whatever the branch points at by now
    $commitish = if ($env:GITHUB_SHA) { @("--targetCommitish", $env:GITHUB_SHA) } else { @() }
    vpk upload github --repoUrl $RepoUrl --token $token -o $ReleasesDir --publish --tag "v$newVersion" --releaseName "$AppTitle $newVersion" @commitish
    if ($LASTEXITCODE -ne 0) { throw "vpk upload failed" }
}

Write-Host "Built $AppName version $newVersion"
