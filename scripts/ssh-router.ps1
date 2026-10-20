# WindowsTool card: SSH channel for the soft router's Hermes.
#   -Action start   install/refresh the OpenSSH server config + start sshd
#   -Action stop    stop the sshd service
#   -Action status  print service / port / key state
#
# Self-elevates once (UAC) because installing or starting the service needs admin.
# ASCII-only on purpose: PowerShell 5.1 parses BOM-less UTF-8 .ps1 as GBK and
# Chinese text there breaks the parser (see windows-tool-console skill).

param([string]$Action = 'start')

$ErrorActionPreference = 'Continue'
$routerKey = 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIAWahbnUg2lW0/mbxXWjCi2xUblGV6y0u2yfHFoxF+jN router-to-pc'
$sshDir = Join-Path $env:ProgramData 'ssh'
$sshdExe = Join-Path $env:windir 'System32\OpenSSH\sshd.exe'
$keygenExe = Join-Path $env:windir 'System32\OpenSSH\ssh-keygen.exe'
$selfPath = $MyInvocation.MyCommand.Path

function Test-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  return (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Elevated {
  if (Test-Admin) { return $true }
  Write-Host "Admin rights needed - relaunching elevated (one UAC)..."
  try {
    Start-Process powershell -Verb RunAs -ArgumentList @(
      '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $selfPath, '-Action', $Action) -ErrorAction Stop
  } catch {
    Write-Host "Elevation cancelled: $($_.Exception.Message)"
  }
  return $false
}

function Show-Status {
  $svc = Get-Service sshd -ErrorAction SilentlyContinue
  if ($svc) { Write-Host ("sshd service : " + $svc.Status + " / " + $svc.StartType) }
  else { Write-Host "sshd service : not installed" }
  $listen = (Get-NetTCPConnection -LocalPort 22 -State Listen -ErrorAction SilentlyContinue | Measure-Object).Count
  Write-Host ("port 22      : " + $listen + " listener(s)")
  $akf = Join-Path $sshDir 'administrators_authorized_keys'
  if (Test-Path $akf) {
    try {
      $has = ((Get-Content $akf -Raw -ErrorAction Stop) -match 'router-to-pc')
      Write-Host ("router key  : " + $has)
    } catch {
      Write-Host "router key  : present (admin needed to read)"
    }
  }
  else { Write-Host "router key  : missing" }
}

function Invoke-Start {
  if (-not (Test-Path $sshdExe)) {
    Write-Host "OpenSSH Server not installed - installing the Windows capability (needs internet)..."
    $cap = Get-WindowsCapability -Online -Name 'OpenSSH.Server*'
    if ($cap.State -ne 'Installed') { Add-WindowsCapability -Online -Name $cap.Name | Out-Null }
  }

  New-Item -ItemType Directory -Force -Path $sshDir | Out-Null
  if (-not (Test-Path (Join-Path $sshDir 'ssh_host_ed25519_key'))) {
    Write-Host "Generating host keys..."
    & $keygenExe -A 2>&1 | Out-Null
  }

  $cfg = Join-Path $sshDir 'sshd_config'
  if (-not (Test-Path $cfg)) {
    Write-Host "Writing sshd_config (key-only, PowerShell default shell)..."
    @"
Port 22
PubkeyAuthentication yes
PasswordAuthentication no
PermitRootLogin no
Subsystem sftp sftp-server.exe
Match Group administrators
       AuthorizedKeysFile __PROGRAMDATA__/ssh/administrators_authorized_keys
"@ | Set-Content -Path $cfg -Encoding ASCII
  }

  # Linusonn is a local admin, so sshd only reads administrators_authorized_keys.
  $akf = Join-Path $sshDir 'administrators_authorized_keys'
  $cur = (Get-Content $akf -Raw -ErrorAction SilentlyContinue)
  if (-not $cur -or $cur -notmatch 'router-to-pc') {
    Set-Content -Path $akf -Value $routerKey -Encoding ASCII
  }
  icacls $akf /inheritance:r /grant "Administrators:F" /grant "SYSTEM:F" | Out-Null

  $rk = 'HKLM:\SOFTWARE\OpenSSH'
  if (-not (Test-Path $rk)) { New-Item -Path $rk -Force | Out-Null }
  Set-ItemProperty -Path $rk -Name DefaultShell -Value (Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe')
  Set-ItemProperty -Path $rk -Name DefaultShellCommandOption -Value '-c'

  Set-Service -Name sshd -StartupType Automatic
  try { Start-Service sshd -ErrorAction Stop } catch { Write-Host ("Start-Service: " + $_.Exception.Message) }
  Start-Sleep -Seconds 2
}

function Invoke-Stop {
  try { Stop-Service sshd -Force -ErrorAction Stop; Write-Host "sshd stopped" }
  catch { Write-Host ("Stop-Service: " + $_.Exception.Message) }
}

switch ($Action.ToLower()) {
  'status' { Show-Status; break }
  'stop'   { if (Ensure-Elevated) { Invoke-Stop; Show-Status }; break }
  default  { if (Ensure-Elevated) { Invoke-Start; Show-Status }; break }
}
