# Dumps the IL of the CombatHUD / WeaponManager members the mod replicates, so the
# claims in the mod's comments can be checked against the shipped assembly.
param(
    [string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Nuclear Option'
)

Add-Type -Path (Join-Path $GameRoot 'BepInEx\core\Mono.Cecil.dll')

$managed = Join-Path $GameRoot 'NuclearOption_Data\Managed\Assembly-CSharp.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($managed)

function Dump-Method([string]$TypeName, [string]$MethodName) {
    $type = $asm.MainModule.GetType($TypeName)
    if ($null -eq $type) { Write-Output "!! no type $TypeName"; return }
    $methods = @($type.Methods | Where-Object { $_.Name -eq $MethodName })
    if ($methods.Count -eq 0) { Write-Output "!! no method $TypeName.$MethodName"; return }
    foreach ($m in $methods) {
        Write-Output ("=== {0}.{1} : {2}" -f $TypeName, $m.Name, $m.ReturnType.Name)
        if (-not $m.HasBody) { Write-Output '  (no body)'; continue }
        foreach ($i in $m.Body.Instructions) {
            Write-Output ("  {0} {1}" -f $i.OpCode.Name, $i.Operand)
        }
    }
}

Write-Output '--- CombatHUD AudioClip fields'
$hud = $asm.MainModule.GetType('CombatHUD')
$hud.Fields | Where-Object { $_.FieldType.Name -like '*AudioClip*' } |
    ForEach-Object { Write-Output ("  {0} : {1} (private={2})" -f $_.Name, $_.FieldType.FullName, $_.IsPrivate) }

Write-Output '--- CombatHUD.SelectUnit / DeSelectUnit / DeselectAll'
Dump-Method 'CombatHUD' 'SelectUnit'
Dump-Method 'CombatHUD' 'DeSelectUnit'
Dump-Method 'CombatHUD' 'DeselectAll'
Dump-Method 'CombatHUD' 'TargetSelect'

Write-Output '--- WeaponManager target list members'
$wm = $asm.MainModule.GetType('WeaponManager')
$wm.Methods | Where-Object { $_.Name -like '*Target*' } | ForEach-Object { Write-Output ("  {0}({1})" -f $_.Name, (($_.Parameters | ForEach-Object { $_.ParameterType.Name }) -join ', ')) }

Write-Output '--- WeaponManager.AddTargetList / RemoveTargetList / TargetListChanged'
Dump-Method 'WeaponManager' 'AddTargetList'
Dump-Method 'WeaponManager' 'RemoveTargetList'
Dump-Method 'WeaponManager' 'TargetListChanged'

Write-Output '--- HUDUnitMarker.SelectMarker / DeselectMarker'
Dump-Method 'HUDUnitMarker' 'SelectMarker'
Dump-Method 'HUDUnitMarker' 'DeselectMarker'

Write-Output '--- DynamicMap icon members'
Dump-Method 'DynamicMap' 'SelectIcon'
Dump-Method 'DynamicMap' 'DeselectIcon'
Dump-Method 'DynamicMap' 'DeselectAllIcons'

Write-Output '--- MapIcon / UnitMapIcon / HUDUnitMarker fields'
foreach ($name in @('MapIcon', 'UnitMapIcon', 'HUDUnitMarker')) {
    $t = $asm.MainModule.GetType($name)
    Write-Output ("  [{0}]" -f $name)
    $t.Fields | ForEach-Object { Write-Output ("    {0} {1} : {2}" -f $_.Attributes, $_.Name, $_.FieldType.FullName) }
}

Write-Output '--- UnitMapIcon.UpdateVisibility / HUDUnitMarker.UpdateVisibility'
Dump-Method 'UnitMapIcon' 'UpdateVisibility'
Dump-Method 'HUDUnitMarker' 'UpdateVisibility'

Write-Output '--- CombatHUD.SetAircraft / GetTargetList'
Dump-Method 'CombatHUD' 'SetAircraft'
Dump-Method 'CombatHUD' 'GetTargetList'
