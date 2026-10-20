# Compile-check the project's scripts without opening Unity.
#
# Finds an installed Unity editor, takes its engine + reference assemblies and
# runs the Roslyn compiler that ships with it. Reports errors only, so it can
# be used as a fast "did I break the code?" gate before opening the editor.
#
#   powershell -File Tools/check_compile.ps1
#   powershell -File Tools/check_compile.ps1 -Warnings
param(
    [string]$ScriptsDir = (Join-Path $PSScriptRoot '..\Assets\Resource\Scripts'),
    [string]$OutDir = (Join-Path $PSScriptRoot '..\Temp\script-check'),
    [switch]$Warnings
)

$ErrorActionPreference = 'Stop'

function Find-UnityEditor {
    $roots = @(
        'C:\Program Files\Unity\Hub\Editor',
        'C:\Program Files\Unity\Editor',
        'C:\Program Files (x86)\Unity\Editor'
    )
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        $candidate = Get-ChildItem -LiteralPath $root -Directory |
            Sort-Object Name -Descending |
            Where-Object { Test-Path (Join-Path $_.FullName 'Editor\Data\DotNetSdkRoslyn\csc.dll') } |
            Select-Object -First 1
        if ($candidate) { return (Join-Path $candidate.FullName 'Editor\Data') }
    }
    return $null
}

$unity = Find-UnityEditor
if (-not $unity) { throw 'No Unity editor install found (looked in Unity Hub + standalone paths).' }

$dotnet = Join-Path $unity 'NetCoreRuntime\dotnet.exe'
$csc = Join-Path $unity 'DotNetSdkRoslyn\csc.dll'
foreach ($tool in @($dotnet, $csc)) {
    if (-not (Test-Path -LiteralPath $tool)) { throw "Missing tool: $tool" }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# Engine modules + the BCL shims the engine builds against.
$refs = @()
$refs += Get-ChildItem (Join-Path $unity 'Managed\UnityEngine') -Filter 'UnityEngine*.dll' | ForEach-Object { $_.FullName }
$refs += Get-ChildItem (Join-Path $unity 'NetStandard\compat\2.1.0\shims\netfx') -Filter '*.dll' | ForEach-Object { $_.FullName }
$refs += Join-Path $unity 'NetStandard\ref\2.1.0\netstandard.dll'

# Packages the project already compiled once (UI / TextMeshPro ...), minus our own assemblies.
$scriptAssemblies = Join-Path $PSScriptRoot '..\Library\ScriptAssemblies'
if (Test-Path -LiteralPath $scriptAssemblies) {
    $refs += Get-ChildItem $scriptAssemblies -Filter '*.dll' |
        Where-Object { $_.Name -notlike 'Assembly-CSharp*' } |
        ForEach-Object { $_.FullName }
}

$sources = Get-ChildItem -LiteralPath $ScriptsDir -Recurse -Filter '*.cs'
if (-not $sources) { throw "No .cs files under $ScriptsDir" }

$editorSources = $sources | Where-Object { $_.DirectoryName -match '\\Editor$' }
if ($editorSources) {
    $refs += Join-Path $unity 'Managed\UnityEngine\UnityEditor.dll'
    $refs += Join-Path $unity 'Managed\UnityEngine\UnityEditor.CoreModule.dll'
}

$refs = $refs | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -Unique
$refsFile = Join-Path $OutDir 'refs.rsp'
($refs | ForEach-Object { '-r:"' + $_ + '"' }) | Set-Content -LiteralPath $refsFile -Encoding UTF8

$sourcesFile = Join-Path $OutDir 'sources.rsp'
($sources | ForEach-Object { '"' + $_.FullName + '"' }) | Set-Content -LiteralPath $sourcesFile -Encoding UTF8

$assembly = Join-Path $OutDir 'Assembly-CSharp.check.dll'
$output = & $dotnet $csc -nologo -noconfig -nostdlib+ -target:library -langversion:9 `
    -out:"$assembly" "@$refsFile" "@$sourcesFile" 2>&1

$errors = $output | Where-Object { $_ -match ':\s*error\s' }
$warns = $output | Where-Object { $_ -match ':\s*warning\sCS' }

$version = Split-Path (Split-Path (Split-Path $unity -Parent) -Parent) -Leaf
"compiled $($sources.Count) files against Unity $version"
if ($Warnings -and $warns) { $warns }
if ($errors) {
    "ERRORS:"
    $errors
    exit 1
}
"no compile errors"
exit 0
