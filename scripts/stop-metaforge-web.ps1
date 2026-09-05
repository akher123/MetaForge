# Stops running MetaForge.Web instances so rebuilds can overwrite output DLLs.
Get-Process -Name 'MetaForge.Web' -ErrorAction SilentlyContinue | Stop-Process -Force

Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
    Where-Object {
        $cmd = $_.CommandLine
        if ([string]::IsNullOrWhiteSpace($cmd)) { return $false }
        if ($cmd -match '\b(build|ef|test|restore|publish)\b') { return $false }
        return $cmd -like '*MetaForge.Web*'
    } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Start-Sleep -Seconds 1
