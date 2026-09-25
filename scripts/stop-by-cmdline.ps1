param(
    [Parameter(Mandatory = $true)][string]$Pattern
)
# 按命令行关键字结束进程（用于 bat 拉起来的 python 子进程，杀 cmd 杀不掉它）
$killed = 0
Get-CimInstance Win32_Process -Filter "Name='python.exe' or Name='pythonw.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine -like "*$Pattern*" } |
    ForEach-Object {
        try {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop
            Write-Output ("killed PID {0}" -f $_.ProcessId)
            $killed++
        } catch { }
    }
if ($killed -eq 0) { Write-Output "no process matched '$Pattern'" }
