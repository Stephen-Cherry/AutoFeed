# Read the version number from AutoFeed.csproj
[xml]$csproj = Get-Content 'AutoFeed.csproj'
$version = $csproj.Project.PropertyGroup.Version

# Load the manifest.json file
$manifest = Get-Content 'manifest.json' | ConvertFrom-Json

# Update the version_number in the manifest.json
$manifest.version_number = $version

# Save the updated manifest.json
$manifest | ConvertTo-Json | Set-Content 'manifest.json'