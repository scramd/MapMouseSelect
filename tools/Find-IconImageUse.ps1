# Finds every method in Assembly-CSharp that touches a MapIcon's iconImage, to see
# whether the game ever hides a unit map icon rather than removing it.
param(
    [string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Nuclear Option',
    [string]$MemberName = 'iconImage'
)

Add-Type -Path (Join-Path $GameRoot 'BepInEx\core\Mono.Cecil.dll')

$managed = Join-Path $GameRoot 'NuclearOption_Data\Managed\Assembly-CSharp.dll'
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($managed)

$types = $asm.MainModule.GetTypes()
$hits = 0

foreach ($t in $types) {
    if ($t.FullName -like 'PlayerList*' -or $t.Name -like '<*') { continue }
    foreach ($m in @($t.Methods)) {
        if (-not $m.HasBody) { continue }
        $touched = @($m.Body.Instructions | Where-Object {
                $_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.Name -eq $MemberName
            })
        if ($touched.Count -eq 0) { continue }

        $hits++
        Write-Output ("=== {0}.{1}" -f $t.FullName, $m.Name)
        foreach ($i in $m.Body.Instructions) {
            $op = $i.Operand
            if ($op -is [Mono.Cecil.FieldReference] -or $op -is [Mono.Cecil.MethodReference] -or $op -is [string]) {
                Write-Output ("  {0} {1}" -f $i.OpCode.Name, $op)
            } elseif ($null -ne $op) {
                Write-Output ("  {0} {1}" -f $i.OpCode.Name, $op)
            } else {
                Write-Output ("  {0}" -f $i.OpCode.Name)
            }
        }
    }
}

Write-Output ("methods touching {0}: {1}" -f $MemberName, $hits)
