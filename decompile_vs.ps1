if (!(Test-Path -Path $env:VINTAGE_STORY)) {
    echo "Creating backup folder"
    mkdir $env:VINTAGE_STORY\bak
    echo "Copying PDB symbols from vintage story folder into backup folder"
    Get-ChildItem -Path "$env:VINTAGE_STORY" -Filter *.pdb | ForEach-Object { mv $_.FullName "$env:VINTAGE_STORY\bak\" }
}
echo "Decompiling Vintage Story DLLs"
if (Test-Path -Path "$env:VINTAGE_STORY\decompiled") {
    rm "$env:VINTAGE_STORY\decompiled" -Recurse -Force
}
mkdir "$env:VINTAGE_STORY\decompiled"
Get-ChildItem -Path "$env:VINTAGE_STORY" -Filter *.dll | ForEach-Object { ilspycmd $_.FullName -o "$env:VINTAGE_STORY\decompiled" -d -usepdb -genpdb }
echo "Copying decompiled Vintage Story DLLs to base folder"
Get-ChildItem -Path "$env:VINTAGE_STORY\decompiled" -Filter *.pdb -Recurse -Force | ForEach-Object { cp $_.FullName $env:VINTAGE_STORY }