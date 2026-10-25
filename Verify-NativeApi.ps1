param([string]$GameDir = $env:DICEOMANCER_GAME_DIR)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw 'Pass -GameDir or set DICEOMANCER_GAME_DIR to your game directory.' }
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Diceomancer_Data\Managed\Assembly-CSharp.dll'))
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $PSScriptRoot 'src\bin\Release\DEDEBUG.dll'))
try {
    function Expand-ApiTypes($Types) { foreach ($type in $Types) { $type; Expand-ApiTypes $type.NestedTypes } }
    $gameTypes = @(Expand-ApiTypes $assembly.MainModule.Types)
    $pluginTypes = @(Expand-ApiTypes $plugin.MainModule.Types)
    # Keep static verification independent of the old test/export project.
    $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src\CommandDefinitions.cs') -Raw
    $manifest = @(foreach ($line in $source -split '\r?\n') {
        if ($line -match '^\s*new CommandSpec\("([^"\r\n]+)"') {
            $name = $Matches[1]
            $native = $null
            if ($line -match 'native:\s*"([^"\r\n]+)"' -or $line -match 'ArgumentKind\.\w+,\s*(?:true|false),\s*"([^"\r\n]+)"') { $native = $Matches[1] }
            [pscustomobject]@{Name=$name; Native=$native; Panel=($line -match 'panel:\s*true')}
        }
    })
    $registration = ($pluginTypes | Where-Object FullName -eq 'DEDEBUG.CommandDefinitions').Methods | Where-Object Name -eq 'Create'
    $constructors = @($registration.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Newobj -and $_.Operand.DeclaringType.FullName -eq 'DEDEBUG.CommandSpec'
    })
    if ($constructors.Count -ne $manifest.Count) { throw 'Source registrations do not match the compiled plugin. Keep command registrations one per line.' }
    $compiledStrings = @($registration.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldstr } | ForEach-Object Operand)
    foreach ($command in $manifest) {
        if ($command.Name -cnotin $compiledStrings) { throw "Command name missing from compiled plugin: $($command.Name)" }
    }
    $nativeCommands = @($manifest | Where-Object Native | ForEach-Object Native | Sort-Object -Unique)
    if ($manifest.Count -lt 32 -or @($manifest | Where-Object Panel).Count -ne 5 -or @($manifest | Group-Object Name).Count -ne $manifest.Count) { throw 'Existing command scopes or name uniqueness changed unexpectedly.' }
    $receiver = $assembly.MainModule.Types | Where-Object FullName -eq 'UI.DebugRedeemCode'
    foreach ($name in $nativeCommands) {
        if (@($receiver.Methods | Where-Object {
            $_.Name -eq $name -and $_.IsPublic -and $_.ReturnType.FullName -eq 'System.Void' -and
            $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'System.String'
        }).Count -ne 1) { throw "Native command signature changed: $name" }
    }
    $entry = $plugin.MainModule.Types | Where-Object FullName -eq 'DEDEBUG.DedebugPlugin'
    $identity = @($entry.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin' })
    if ($identity.Count -ne 1 -or $identity[0].ConstructorArguments[0].Value -ne 'diceomancer.dedebug') { throw 'Unexpected plugin GUID.' }
    if ($plugin.MainModule.AssemblyReferences.Name -contains 'BalanceTuner') { throw 'DEDEBUG must be independent of BalanceTuner.' }
    if ($pluginTypes.FullName -contains 'DEDEBUG.ConsoleCommandValidation') { throw 'Old success/failure observer must be removed.' }
    $references = @($pluginTypes.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions } |
        ForEach-Object { $_.Operand } | Where-Object {
            $_ -is [Mono.Cecil.MemberReference] -and $_.DeclaringType -and $_.DeclaringType.Scope.Name -eq 'Assembly-CSharp'
        } | Sort-Object FullName -Unique)
    $definitions = @($gameTypes | ForEach-Object { $_.Methods; $_.Fields } | ForEach-Object FullName)
    foreach ($reference in $references) {
        if ($reference.FullName -cnotin $definitions) { throw "Game API changed: $($reference.FullName)" }
    }
    $hooks = @(
        'System.Void Command.PlayerCommandHandler::AddCommand(Command.ICommand,System.Boolean)',
        'System.Void Command.PlayerCommandHandler::InsertCommand(Command.ICommand,System.Boolean)',
        'System.Void Command.PlayerCommandHandler::EnqueueSubCommand(Command.SubCommand,System.Boolean)',
        'System.Void World.HelperManager::ShowHelperContent(System.String)',
        'System.Void Utilities.GameManager::TryToUnpauseGame()',
        'System.Void World.MapPlayerTracker::SelectNode(World.NewMap.MapNode)'
    )
    foreach ($hook in $hooks) { if ($hook -cnotin $definitions) { throw "Native hook changed: $hook" } }
    foreach ($field in @('UI.DebugRedeemCode Utilities.LogManager::debugRedeemCode',
        'System.Collections.Generic.Dictionary`2<System.String,Card.CardDateCollection> GameData.CardDataManager::_cardDateCollectionDictionary',
        'System.Collections.Generic.Queue`1<Command.SubCommand> Command.PlayerCommandHandler::_subCommandsQueue',
        'System.Int32 World.WorldManager::_worldTurnCount', 'System.Int32 World.WorldManager::_corruptionPauseCounter')) {
        if ($field -cnotin $definitions) { throw "Reflected game field changed: $field" }
    }
    foreach ($contract in @(@('Card.ColorE','Void',4), @('Combat.BattleWorld.BattlePhase','Main',1), @('World.CampaignState','InsideBattle',0),
        @('World.CampaignState','Traveling',3), @('Combat.DamageSourceType','IndirectDamage',1), @('Combat.DamageSpecialType','Standard',0))) {
        $type = $gameTypes | Where-Object FullName -eq $contract[0]
        $field = $type.Fields | Where-Object Name -eq $contract[1]
        if (-not $field -or -not $field.HasConstant -or $field.Constant -ne $contract[2]) { throw "Native enum changed: $contract" }
    }
    $inputManifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot '.build\diagnostics\InputManifest.json') -Raw | ConvertFrom-Json
    $compiledManifest = $pluginTypes | Where-Object FullName -eq 'DEDEBUG.GameInputManifest'
    $moduleId = ($compiledManifest.Fields | Where-Object Name -eq 'ModuleId').Constant
    if ($moduleId -ne $assembly.MainModule.Mvid.ToString() -or $inputManifest.ModuleId -ne $moduleId) { throw 'Game input manifest version mismatch.' }
    $gate = $pluginTypes | Where-Object FullName -eq 'DEDEBUG.GameInputGate'
    $guardSignatures = @($gate.Methods | Where-Object { $_.IsPublic -and $_.IsStatic } | ForEach-Object { $_.FullName.Replace('DEDEBUG.GameInputGate','UnityEngine.Input') })
    $inputMethods = @(foreach ($type in $gameTypes) {
        foreach ($method in $type.Methods | Where-Object HasBody) {
            $reads = @($method.Body.Instructions | ForEach-Object { $_.Operand } | Where-Object {
                $_ -is [Mono.Cecil.MethodReference] -and $_.DeclaringType.FullName -eq 'UnityEngine.Input' -and -not $_.Name.StartsWith('set_')
            })
            foreach ($read in $reads) { if ($read.FullName -cnotin $guardSignatures) { throw "Input read has no compatible guard: $($read.FullName)" } }
            if ($reads.Count -gt 0) { $method }
        }
    })
    $expectedTokens = @($inputMethods | ForEach-Object { $_.MetadataToken.ToInt32() } | Sort-Object)
    if (Compare-Object $expectedTokens @($inputManifest.Targets.Token | Sort-Object)) { throw 'Input manifest does not cover every game input read method.' }
    $tokenBlobRef = ($compiledManifest.Methods | Where-Object Name -eq '.cctor').Body.Instructions |
        ForEach-Object { $_.Operand } | Where-Object { $_ -is [Mono.Cecil.FieldReference] -and $_.DeclaringType.Name -eq '<PrivateImplementationDetails>' } | Select-Object -First 1
    $tokenBlob = ($pluginTypes.Fields | Where-Object FullName -eq $tokenBlobRef.FullName).InitialValue
    $compiledTokens = @(for ($offset=0; $offset -lt $tokenBlob.Length; $offset+=4) { [BitConverter]::ToInt32($tokenBlob,$offset) })
    if (Compare-Object $expectedTokens @($compiledTokens | Sort-Object)) { throw 'Compiled input token table does not match the verified game methods.' }
    $report = [ordered]@{RegisteredCommands=$manifest.Count; NativeCommands=$nativeCommands; GameMembers=@($references.FullName);
        NativeHooks=$hooks; InputModuleId=$moduleId; InputReadMethods=@($inputMethods.FullName); InputWrapperSignatures=$guardSignatures;
        OutcomeJudgmentRemoved=$true; IndependentPlugin=$true}
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot '.build\diagnostics\NativeApiReport.json') -Encoding utf8
    "PASS: $($manifest.Count) registered commands, $($nativeCommands.Count) native signatures, $($references.Count) game members, $($hooks.Count) hooks; all $($inputMethods.Count) game input read methods covered; no success/failure judgment."
} finally { $plugin.Dispose(); $assembly.Dispose() }
