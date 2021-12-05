param ($vertype = 'patch')

Set-StrictMode -Version Latest

write-host "version type $vertype"

Remove-Item .\tmp_release -Force  -Recurse
New-Item .\tmp_release -ItemType "directory" -Force

$manifestContent =  Get-Content -path .\manifest.json -Raw
$j = $manifestContent | ConvertFrom-Json

$sourceFileContent = Get-Content -path .\PersonalLogisticsPlugin.cs -Raw
$sourceFileContent -match '.*PluginVersion = "(\d+.\d+.\d+)".*'

$old_vernum = $Matches[1]

$v = [version]$old_vernum
write-host "v = $v"

if ($vertype -eq "minor")
{
    $new_version = [version]::New($v.Major, $v.Minor + 1, 0)
}
elseif ($vertype -eq "patch")
{
    $new_version = [version]::New($v.Major, $v.Minor, $v.Build + 1)
}
elseif ($vertype -eq "major")
{
    $new_version = [version]::New($v.Major + 1, 0, 0)
}
else
{
    Write-Host "invalid vertype: should be (major, minor, patch), got $vertype"
    exit
}

Write-Host "next version $new_version"
$new_version_string = "$([string]::Join(".", $new_version))";

$sourceFileContent -replace $old_vernum, $new_version_string  | Set-Content -Path .\PersonalLogisticsPlugin.cs -NoNewline

Import-Module -Name ".\Invoke-MsBuild.psm1"
Invoke-MsBuild -Path ".\PersonalLogistics.sln"

Copy-Item -Path bin/Debug/netstandard2.0/PersonalLogistics.dll -Destination tmp_release
Copy-Item readme.md -Destination tmp_release\README.md
Copy-Item icon.png -Destination tmp_release
Copy-Item pls -Destination tmp_release
Copy-Item pui -Destination tmp_release

$j.version_number = $new_version_string
$j |ConvertTo-Json | Set-Content -Path .\tmp_release\manifest.json

$compress = @{
    Path = "tmp_release\*"
    CompressionLevel = "Fastest"
    DestinationPath = "tmp_release\PersonalLogistics.zip"
}
Compress-Archive @compress

Copy-Item .\tmp_release\manifest.json manifest.json