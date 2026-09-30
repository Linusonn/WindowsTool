param(
    [Parameter(Mandatory = $true)][string]$Pattern,
    # restrict to these process names (with or without .exe); omit = scan all processes
    [string[]]$Names
)
# Kill processes whose command line contains $Pattern.
# Needed for children started by a .bat (killing cmd.exe leaves java/node alive).
#   stop-by-cmdline.ps1 -Pattern rtd-backend.jar
#   stop-by-cmdline.ps1 -Pattern "ComfyUI\main.py" -Names python,pythonw
$nameSet = $null
if ($Names) {
    # accept "a,b,c" and "a b c" alike (PowerShell -File does not split arrays)
    $nameSet = $Names | ForEach-Object { $_ -split ',' } |
        ForEach-Object { $_.Trim() -replace '\.exe$', '' } |
        Where-Object { $_ }
}

$killed = 0
Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object {
        $_.ProcessId -ne $PID -and
        $_.CommandLine -and
        $_.CommandLine -like "*$Pattern*" -and
        ($null -eq $nameSet -or $nameSet -contains ($_.Name -replace '\.exe$', ''))
    } |
    ForEach-Object {
        try {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop
            Write-Output ("killed {0} (PID {1})" -f $_.Name, $_.ProcessId)
            $killed++
        } catch { }
    }
if ($killed -eq 0) { Write-Output "no process matched '$Pattern'" }
