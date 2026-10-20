param([string]$ValidationProject)
$ErrorActionPreference = 'Stop'
$resourceRoot = Split-Path $PSScriptRoot -Parent
$projectRoot = [IO.Path]::GetFullPath((Join-Path $resourceRoot '../..'))
if (-not $ValidationProject) { $ValidationProject = Join-Path $env:TEMP ('EchoLoginValidation-' + [guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Force -Path "$ValidationProject/Assets/Resource", "$ValidationProject/Packages", "$ValidationProject/ProjectSettings" | Out-Null
foreach ($folder in @('Scripts','Fonts','Models','Shaders')) {
    Copy-Item -LiteralPath (Join-Path $resourceRoot $folder) -Destination "$ValidationProject/Assets/Resource" -Recurse -Force
}
$sourceManifest = Get-Content -LiteralPath "$projectRoot/Packages/manifest.json" -Raw | ConvertFrom-Json
$deps = [ordered]@{}
foreach ($dependency in $sourceManifest.dependencies.PSObject.Properties) {
    if ($dependency.Name.StartsWith('com.unity.modules.')) { $deps[$dependency.Name] = $dependency.Value }
}
$deps['com.unity.modules.imgui'] = '1.0.0'
$deps['com.unity.ugui'] = 'file:' + ($projectRoot.Replace('\','/') + '/Library/PackageCache/com.unity.ugui@1.0.0')
$deps['com.unity.textmeshpro'] = 'file:' + ($projectRoot.Replace('\','/') + '/Library/PackageCache/com.unity.textmeshpro@3.0.6')
$deps['com.unity.nuget.newtonsoft-json'] = 'file:' + ($projectRoot.Replace('\','/') + '/Library/PackageCache/com.unity.nuget.newtonsoft-json@3.0.2')
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText("$ValidationProject/Packages/manifest.json", (@{dependencies=$deps} | ConvertTo-Json -Depth 5), $utf8)
Copy-Item -LiteralPath "$projectRoot/ProjectSettings/ProjectVersion.txt" -Destination "$ValidationProject/ProjectSettings/ProjectVersion.txt"
Write-Output $ValidationProject
