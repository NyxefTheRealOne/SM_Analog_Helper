function Get-AnalogDriveInputPath {
    [CmdletBinding()]
    param([string]$HelperRoot, [string]$ModPath, [string]$UserRoot, [string[]]$SteamRoots)
    $modId = '60fb9be7-bdf4-4d6e-bbdb-51c404480cf5'
    function Test-AnalogMod([string]$Folder) {
        try {
            $metadata = Get-Content -LiteralPath (Join-Path $Folder 'description.json') -Raw | ConvertFrom-Json
            return $metadata.localId -eq $modId
        } catch { return $false }
    }
    if ($ModPath) {
        $ModPath = (Resolve-Path -LiteralPath $ModPath).Path
        if (-not (Test-AnalogMod $ModPath)) { throw 'That folder is not the Analog Drive mod. Choose the folder containing its description.json.' }
        return Join-Path $ModPath 'Controller\input.json'
    }
    # The launcher shipped inside an installed mod always feeds that exact copy.
    $parent = Split-Path $HelperRoot -Parent
    if ((Test-AnalogMod $parent) -and -not (Test-Path -LiteralPath (Join-Path $parent 'Tools\Install.ps1'))) {
        return Join-Path $parent 'Controller\input.json'
    }
    if (-not $UserRoot) { $UserRoot = Join-Path $env:APPDATA 'Axolot Games\Scrap Mechanic\User' }
    $candidates = New-Object 'Collections.Generic.List[string]'
    foreach ($profile in @(Get-ChildItem -LiteralPath $UserRoot -Directory -Filter 'User_*' -ErrorAction SilentlyContinue)) {
        foreach ($folder in @(Get-ChildItem -LiteralPath (Join-Path $profile.FullName 'Mods') -Directory -ErrorAction SilentlyContinue)) {
            if (Test-AnalogMod $folder.FullName) { $candidates.Add($folder.FullName) }
        }
    }
    if (-not $SteamRoots) {
        $registry = Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
        $SteamRoots = @($registry.SteamPath, (Join-Path ${env:ProgramFiles(x86)} 'Steam'), (Join-Path $env:ProgramFiles 'Steam')) |
            Where-Object { $_ } | Select-Object -Unique
    }
    $libraries = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($steamRoot in $SteamRoots) {
        [void]$libraries.Add($steamRoot)
        $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            $content = Get-Content -LiteralPath $vdf -Raw
            foreach ($match in [regex]::Matches($content, '"path"\s+"([^"]+)"')) {
                [void]$libraries.Add($match.Groups[1].Value.Replace('\\','\'))
            }
        }
    }
    foreach ($library in $libraries) {
        $contentRoot = Join-Path $library 'steamapps\workshop\content\387990'
        foreach ($folder in @(Get-ChildItem -LiteralPath $contentRoot -Directory -ErrorAction SilentlyContinue)) {
            if (Test-AnalogMod $folder.FullName) { $candidates.Add($folder.FullName) }
        }
    }
    $found = @($candidates | Select-Object -Unique | ForEach-Object {
        $request = Get-Item -LiteralPath (Join-Path $_ 'Controller\helper-request.json') -ErrorAction SilentlyContinue
        $description = Get-Item -LiteralPath (Join-Path $_ 'description.json')
        [pscustomobject]@{ Folder=$_; Requested=($null -ne $request);
            Time=if ($request) { $request.LastWriteTimeUtc } else { $description.LastWriteTimeUtc } }
    } | Sort-Object @{Expression='Requested';Descending=$true},@{Expression='Time';Descending=$true})
    if ($found.Count -eq 0) {
        throw 'Analog Drive was not found. Subscribe/install the mod, enter its seat once, then restart this helper. For a custom Steam location, use -ModPath with the mod folder.'
    }
    if ($found.Count -gt 1) {
        Write-Host 'Multiple mod copies found; using the most recently requested seat/mod. Use -ModPath to override.'
    }
    return Join-Path $found[0].Folder 'Controller\input.json'
}
