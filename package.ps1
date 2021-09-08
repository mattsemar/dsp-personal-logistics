Remove-Item .\tmp_release -Force  -Recurse
New-Item .\tmp_release -ItemType "directory" -Force 

$manifestContent =  Get-Content -path .\manifest.json -Raw
$j = $manifestContent | ConvertFrom-Json

$old_vernum = $j.version_number

$v = [version]$old_vernum

$new_version = [version]::New($v.Major,$v.Minor,$v.Build+1)


$new_version_string = "$([string]::Join(".", $new_version))";
$new_version_string

$sourceFileContent = Get-Content -path .\PersonalLogisticsPlugin.cs -Raw
$sourceFileContent -match '.*PluginVersion = "(\d+.\d+.\d+)".*'

$Matches[1]
$sourceFileContent -replace $Matches[1], $new_version_string  | Set-Content -Path .\PersonalLogisticsPlugin.cs

Import-Module -Name ".\Invoke-MsBuild.psm1"
Invoke-MsBuild -Path ".\PersonalLogistics.sln"

Copy-Item -Path bin/Debug/netstandard2.0/PersonalLogistics.dll -Destination tmp_release
Copy-Item readme.md -Destination tmp_release\README.md
Copy-Item icon.png -Destination tmp_release

$j.version_number = $new_version_string
$j |ConvertTo-Json | Set-Content -Path .\tmp_release\manifest.json

$compress = @{
    Path = "tmp_release\*.*"
    CompressionLevel = "Fastest"
    DestinationPath = "tmp_release\PersonalLogistics.zip"
}
Compress-Archive @compress

Copy-Item .\tmp_release\manifest.json manifest.json