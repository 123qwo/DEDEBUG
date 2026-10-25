param(
    [string]$GameDir = $env:DICEOMANCER_GAME_DIR,
    [string]$DotnetPath,
    [string]$PackageSource,
    [switch]$Verify
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw 'Pass -GameDir or set DICEOMANCER_GAME_DIR to your game directory.' }
$GameDir = (Resolve-Path -LiteralPath $GameDir -ErrorAction Stop).Path
if (-not $DotnetPath) {
    $userSdk = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
    if ((Test-Path -LiteralPath $userSdk) -and (& $userSdk --list-sdks)) { $DotnetPath = $userSdk }
    else { $DotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
}
if (-not (& $DotnetPath --list-sdks)) { throw 'A .NET SDK is required.' }
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build\home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.build\packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
& (Join-Path $PSScriptRoot 'Generate-InputManifest.ps1') -GameDir $GameDir
$buildArguments = @('build', (Join-Path $PSScriptRoot 'src\DEDEBUG.csproj'), '-c', 'Release', "-p:GameDir=$GameDir", '--nologo')
if ($PackageSource) { $buildArguments += @('--source', $PackageSource) }
& $DotnetPath @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'DEDEBUG build failed.' }
if ($Verify) {
    & (Join-Path $PSScriptRoot 'Verify-NativeApi.ps1') -GameDir $GameDir
}
