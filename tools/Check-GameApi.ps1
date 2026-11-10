param([Parameter(Mandatory=$true)][string]$ManagedDir, [Parameter(Mandatory=$true)][string]$BepInExCore)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $BepInExCore 'Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ManagedDir 'Assembly-CSharp.dll'))
function Method($asm, $type, $name, $argsExpected, $returnExpected = 'System.Void') {
    $t = $asm.MainModule.GetType($type)
    if (!$t) { throw "Missing type $type" }
    $m = @($t.Methods | Where-Object { $_.Name -eq $name -and (($_.Parameters | ForEach-Object { "$($_.ParameterType.FullName) $($_.Name)" }) -join ',') -eq $argsExpected })
    if ($m.Count -ne 1 -or $m[0].ReturnType.FullName -ne $returnExpected) { throw "Expected unique $type.$name($argsExpected)" }
    Write-Output "Member OK: $type.$name($argsExpected)"
}
Method $game DynamicMap Update ''
Method $game DynamicMap MapControls ''
Method $game DynamicMap DeselectAllIcons ''
Method $game DynamicMap SelectFromMap ''
Method $game MapIcon 'UnityEngine.EventSystems.IPointerClickHandler.OnPointerClick' 'UnityEngine.EventSystems.PointerEventData eventData'
Method $game MapIcon SelectIcon ''
Method $game UnitMapIcon ClickIcon 'MapIcon/ClickSource clickSource'
Method $game CombatHUD DeSelectUnit 'Unit unit'
Method $game CombatHUD SetAircraft 'Aircraft aircraft'
Method $game CombatHUD RemoveAircraft ''
Method $game TargetListSelector Start ''
Method $game TargetListSelector CheckExclusions 'Unit u' 'System.Boolean'
Method $game TargetListSelector ResetFilters ''
Method $game TargetListSelector_ToggleButton TargetListSelector_ToggleButton_OnThemeGroupChanged ''
foreach ($item in @(@($game,'DynamicMap','onAllDeselected'),@($game,'CombatHUD','selectSound'),@($game,'CombatHUD','deselectSound'),@($game,'TargetListSelector_ToggleButton','context'),@($game,'TargetListSelector','resetButtonImage'),@($game,'TargetListSelector','resetButtonLabel'))) {
    $f = $item[0].MainModule.GetType($item[1]).Fields | Where-Object Name -eq $item[2]
    if (!$f) { throw "Missing $($item[1]).$($item[2])" }
    Write-Output "Field OK: $($item[1]).$($item[2]): $($f.FieldType.FullName)"
}
$il = ($game.MainModule.GetType('DynamicMap').Methods | Where-Object Name -eq MapControls).Body.Instructions
$mouse = @($il | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -eq 'System.Boolean UnityEngine.Input::GetMouseButton(System.Int32)' }).Count
$axes = 0; $factors = 0; $leftGate = 0; $rightDown = 0
for ($i = 0; $i -lt $il.Count; $i++) {
    if ($i -gt 0 -and $il[$i].Operand -is [Mono.Cecil.MethodReference] -and $il[$i].Operand.FullName -eq 'System.Boolean UnityEngine.Input::GetMouseButton(System.Int32)' -and $il[$i-1].OpCode.Name -eq 'ldc.i4.0') { $leftGate++ }
    if ($i -gt 0 -and $il[$i].Operand -is [Mono.Cecil.MethodReference] -and $il[$i].Operand.FullName -eq 'System.Boolean UnityEngine.Input::GetMouseButtonDown(System.Int32)') { if ($il[$i-1].OpCode.Name -eq 'ldc.i4.1') { $rightDown++ } else { $rightDown += 100 } }
    if ($il[$i].OpCode.Name -eq 'ldstr' -and $il[$i].Operand -in @('Pan View','Tilt View') -and $il[$i+1].Operand.FullName -eq 'System.Single Rewired.Player::GetAxis(System.String)') { $axes++ }
    if ($i -ge 3 -and $il[$i].Operand -is [Mono.Cecil.MethodReference] -and $il[$i].Operand.FullName -eq 'System.Single UnityEngine.Mathf::Min(System.Single,System.Single)' -and $il[$i-1].OpCode.Name -eq 'ldc.r4' -and [math]::Abs($il[$i-1].Operand - 0.03) -lt 0.000001 -and $il[$i-2].Operand.FullName -eq 'System.Single UnityEngine.Time::get_unscaledDeltaTime()' -and $il[$i-3].Operand -eq 150) { $factors++ }
}
if ($mouse -ne 1 -or $leftGate -ne 1 -or $rightDown -ne 1 -or $axes -ne 2 -or $factors -ne 1) { throw "IL mismatch: mouse=$mouse leftGate=$leftGate rightDown=$rightDown axes=$axes factors=$factors" }
Write-Output 'IL patterns OK: one pan gate, one right-button waypoint read, two mouse axes, one pan factor. This does not run Harmony or the game.'
