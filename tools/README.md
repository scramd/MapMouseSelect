# Tools

Read-only scripts that answer questions about the shipped game build, so a change to it can be traced without opening a decompiler. They read `NuclearOption_Data\Managed\Assembly-CSharp.dll` through the Mono.Cecil that BepInEx ships, and they need the game path, from `NUCLEAR_OPTION_ROOT` or from `-NuclearOptionRoot`.

| Script | What it answers |
|---|---|
| `Find-GameTypes.ps1` | Which types match a regex, with `-Methods` for their methods |
| `Find-MemberUsage.ps1` | Who reads a field or calls a method, given only the name, with the type that declares it. Built for the obfuscated parts of Rewired |
| `Find-StringUsage.ps1` | Which method loads a string constant that matches a regex |
| `Show-VanillaTargetFlow.ps1` | The IL of the `CombatHUD`, `WeaponManager` and `DynamicMap` members this mod mirrors, to check the mod's comments against the build |

The most useful one after a game update is `Find-MemberUsage`, since this mod reaches half of its vanilla members by name and a rename shows up as a warning in the log with no way to find the new name by reading.

## Examples

```powershell
$env:NUCLEAR_OPTION_ROOT = 'D:\SteamLibrary\steamapps\common\Nuclear Option'

.\Find-GameTypes.ps1 -Pattern '^UnitMapIcon$' -Methods
.\Find-MemberUsage.ps1 -Pattern 'iconImage'
.\Find-StringUsage.ps1 -Pattern 'Zoom View'
.\Show-VanillaTargetFlow.ps1
```
