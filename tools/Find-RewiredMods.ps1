# Scans installed BepInEx plugins for APIs that indicate input/settings tampering,
# especially the YawOnMouse-style trick of registering new Rewired actions at runtime.
param(
    [string]$NuclearOptionRoot = $env:NUCLEAR_OPTION_ROOT,
    [string]$BepInExRoot,
    [string[]]$Markers = @(
        'InputManager_Base',
        'actionCategoryMap',
        'actionCategories',
        '_userAssignable',
        'Rewired.Data.UserData',
        'ActionElementMap',
        'ElementAssignment',
        'InputAction',
        'ReInput',
        'UserDataStore'
    )
)

if (-not $BepInExRoot) {
    if (-not $NuclearOptionRoot) {
        throw "Set NUCLEAR_OPTION_ROOT, or pass -NuclearOptionRoot (path to the Nuclear Option install)."
    }
    $BepInExRoot = Join-Path $NuclearOptionRoot 'BepInEx'
}

$skip = @('\\core\\', 'ConfigurationManager', 'MapMouseSelect')

# Known third-party libraries bundled inside mod folders; not interesting and large.
$skipNames = @(
    'Newtonsoft*', 'KeraLua*', 'lua54*', 'Mono.Cecil*', 'System.*', 'Microsoft.*',
    'Unity.*', 'UnityEngine*', '0Harmony*', 'dnlib*', 'BepInEx*', 'Rewired*', 'Mirage*'
)

$dlls = Get-ChildItem $BepInExRoot -Recurse -Filter *.dll -ErrorAction SilentlyContinue |
    Where-Object { $p = $_.FullName; -not ($skip | Where-Object { $p -match $_ }) } |
    Where-Object { $n = $_.Name; -not ($skipNames | Where-Object { $n -like $_ }) }

Write-Output ("Scanning {0} assemblies..." -f $dlls.Count)

foreach ($dll in $dlls) {
    # Decode the whole file in one pass. Per-byte pipeline iteration is orders of magnitude slower.
    $bytes = [System.IO.File]::ReadAllBytes($dll.FullName)
    $text = [System.Text.Encoding]::ASCII.GetString($bytes)

    $hits = @()
    foreach ($m in $Markers) {
        if ($text.Contains($m)) { $hits += $m }
    }

    if ($hits.Count -gt 0) {
        $rel = $dll.FullName.Replace($BepInExRoot, '')
        Write-Output ("{0}`n    -> {1}" -f $rel, ($hits -join ', '))
    }
}
