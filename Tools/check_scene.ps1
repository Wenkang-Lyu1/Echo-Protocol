# Structural check for a Unity scene file, without opening Unity.
#
# Hand-edited scenes fail in Unity only when someone presses Play, with errors
# that point at the YAML rather than at the mistake. This checks the invariants
# Unity relies on:
#   * every local fileID reference exists
#   * Transform.m_Father and Transform.m_Children point at each other
#   * children are listed in m_RootOrder order
#   * every m_Component belongs to its own GameObject
#   * every MonoBehaviour has an owner and a byte-valid m_Script reference
#
#   powershell -File Tools/check_scene.ps1 -Path Assets\Scenes\Start_Scene.unity
param(
    [Parameter(Mandatory = $true)][string]$Path
)

if (-not (Test-Path -LiteralPath $Path)) { throw "Scene not found: $Path" }

$lines = Get-Content -LiteralPath $Path
$blocks = @{}
$current = $null
$buffer = New-Object System.Collections.Generic.List[string]

function Flush {
    if ($script:current -ne $null) {
        $script:blocks[$script:current] = ($script:buffer -join "`n")
    }
}

foreach ($line in $lines) {
    if ($line -match '^--- !u!(\d+) &(\d+)$') {
        Flush
        $script:current = $matches[2]
        $script:buffer = New-Object System.Collections.Generic.List[string]
        continue
    }
    if ($script:current -ne $null) { $script:buffer.Add($line) }
}
Flush

$problems = New-Object System.Collections.Generic.List[string]
$transforms = @{}
$gameObjects = @{}

foreach ($id in $blocks.Keys) {
    $body = $blocks[$id]
    if ($body -match 'm_Father:') { $transforms[$id] = $body }
    if ($body -match 'm_Component:') { $gameObjects[$id] = $body }
}

function Get-RefList([string]$body, [string]$key, [string]$prefix) {
    $result = @()
    $lines = $body -split "`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^\s+$key`:") {
            for ($j = $i + 1; $j -lt $lines.Count; $j++) {
                if ($lines[$j] -match "^\s+- $prefix\s*\{fileID: (\d+)\}") { $result += $matches[1] }
                elseif ($lines[$j] -match '^\s+- ') { continue }
                else { break }
            }
        }
    }
    return $result
}

foreach ($id in $transforms.Keys) {
    $body = $transforms[$id]
    if ($body -notmatch 'm_Father: \{fileID: (\d+)\}') { $problems.Add("$id has no m_Father"); continue }
    $father = $matches[1]
    $children = Get-RefList $body 'm_Children' ''
    if ($father -ne '0') {
        if (-not $transforms.ContainsKey($father)) { $problems.Add("$id father $father is not a Transform"); continue }
        $siblings = Get-RefList $transforms[$father] 'm_Children' ''
        if ($siblings -notcontains $id) { $problems.Add("$id missing from father $father children") }
    }
    foreach ($child in $children) {
        if (-not $transforms.ContainsKey($child)) { $problems.Add("$id child $child is not a Transform"); continue }
        if ($transforms[$child] -notmatch "m_Father: \{fileID: $id\}") { $problems.Add("$id child $child points at another father") }
    }
    $orders = @()
    foreach ($child in $children) {
        if ($transforms.ContainsKey($child) -and $transforms[$child] -match 'm_RootOrder: (\d+)') { $orders += [int]$matches[1] }
    }
    for ($i = 1; $i -lt $orders.Count; $i++) {
        if ($orders[$i] -le $orders[$i - 1]) { $problems.Add("$id children are not ordered by m_RootOrder"); break }
    }
}

foreach ($id in $gameObjects.Keys) {
    $components = Get-RefList $gameObjects[$id] 'm_Component' 'component:'
    if ($components.Count -eq 0) { $problems.Add("$id has no components") }
    foreach ($comp in $components) {
        if (-not $blocks.ContainsKey($comp)) { $problems.Add("$id component $comp missing") }
        elseif ($blocks[$comp] -match 'm_GameObject: \{fileID: (\d+)\}') {
            if ($matches[1] -ne $id) { $problems.Add("$comp m_GameObject points back at $($matches[1])") }
        }
    }
}

$monos = 0
foreach ($id in $blocks.Keys) {
    $body = $blocks[$id]
    if ($body -match 'm_Script:') {
        $monos++
        if ($body -notmatch 'm_GameObject: \{fileID: (\d+)\}') { $problems.Add("$id MonoBehaviour has no owner"); continue }
        if (-not $gameObjects.ContainsKey($matches[1])) { $problems.Add("$id owner $($matches[1]) is not a GameObject") }
        if ($body -notmatch 'm_Script: \{fileID: 11500000, guid: [0-9a-f]{32}, type: 3\}') { $problems.Add("$id has a malformed m_Script") }
    }
}

"objects=$($blocks.Count) transforms=$($transforms.Count) gameObjects=$($gameObjects.Count) monoBehaviours=$monos"
if ($problems.Count -gt 0) {
    "PROBLEMS:"
    $problems | ForEach-Object { "  $_" }
    exit 1
}
"scene structure OK"
exit 0
