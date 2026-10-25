param([string]$GameDir = $env:DICEOMANCER_GAME_DIR)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw 'Pass -GameDir or set DICEOMANCER_GAME_DIR to your game directory.' }
if (Get-Process -Name Diceomancer -ErrorAction SilentlyContinue) { throw 'Exit DICEOMANCER before installing. No process was terminated.' }
$source = Join-Path $PSScriptRoot 'src\bin\Release\DEDEBUG.dll'
if (-not (Test-Path -LiteralPath $source)) { throw 'Build DEDEBUG first.' }
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
function Read-PluginIdentity([string]$Path) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Path)
    try {
        foreach ($type in $assembly.MainModule.Types) {
            foreach ($attribute in $type.CustomAttributes) {
                if ($attribute.AttributeType.FullName -eq 'BepInEx.BepInPlugin') {
                    [pscustomobject]@{Guid=[string]$attribute.ConstructorArguments[0].Value; Version=[string]$attribute.ConstructorArguments[2].Value}
                }
            }
        }
    } finally { $assembly.Dispose() }
}
$identity = @(Read-PluginIdentity $source | Where-Object Guid -eq 'diceomancer.dedebug')
if ($identity.Count -ne 1) { throw 'Unexpected DEDEBUG build identity.' }
$plugins = Join-Path $GameDir 'BepInEx\plugins'
$matches = @(foreach ($file in Get-ChildItem -LiteralPath $plugins -Filter '*.dll' -Recurse) {
    try { if (Read-PluginIdentity $file.FullName | Where-Object Guid -eq 'diceomancer.dedebug') { $file.FullName } }
    catch [BadImageFormatException] { }
})
if ($matches.Count -gt 1) { throw 'Duplicate DEDEBUG installations found.' }
$target = if ($matches.Count -eq 1) { $matches[0] } else { Join-Path $plugins 'DEDEBUG\DEDEBUG.dll' }
if ($matches.Count -eq 0 -and (Test-Path -LiteralPath $target)) { throw 'Target exists with an unknown identity.' }
$otherHashes = @{}
foreach ($file in Get-ChildItem -LiteralPath $plugins -Filter '*.dll' -Recurse) {
    if ($file.FullName -ne $target) { $otherHashes[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
}
$backup = $null
if ($matches.Count -eq 1) {
    $directory = Join-Path $PSScriptRoot ('.local\backups\' + [DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $backup = Join-Path $directory 'DEDEBUG.dll'
    Copy-Item -LiteralPath $target -Destination $backup
}
New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
$expectedHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$staged = $target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    Copy-Item -LiteralPath $source -Destination $staged
    if ((Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Staged DEDEBUG hash mismatch.' }
    Move-Item -LiteralPath $staged -Destination $target -Force
} finally { if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged } }
if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Installed DEDEBUG hash mismatch.' }
foreach ($path in $otherHashes.Keys) {
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $otherHashes[$path]) { throw "Another plugin changed unexpectedly: $path" }
}
$receipt = [ordered]@{InstalledAt=[DateTimeOffset]::Now.ToString('o'); Version=$identity[0].Version; Destination=$target; Backup=$backup; SHA256=$expectedHash; OtherPluginsUnchanged=$true; RuntimeValidation='Pending manual game launch'}
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot '.local') -Force | Out-Null
$receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot '.local\InstallationReceipt.json') -Encoding utf8
$receipt | ConvertTo-Json
