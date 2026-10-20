# WindowsTool launcher for the LegendBox Hyper-V dirty box.
# No UAC needed once this user is a member of the Hyper-V Administrators group;
# until then it self-elevates once (UAC) and continues in the elevated window.
# ASCII-only on purpose: PowerShell 5.1 parses BOM-less UTF-8 .ps1 files as GBK
# and Chinese text there breaks the parser (see windows-tool-console skill).

$vmName = 'LegendBox'
$vmconnect = Join-Path $env:windir 'System32\vmconnect.exe'
$selfPath = $MyInvocation.MyCommand.Path

function Test-HvRights {
  try { $null = Get-VM -Name $vmName -ErrorAction Stop; return $true }
  catch { return $false }
}

if (-not (Test-HvRights)) {
  Write-Host "No Hyper-V rights in this token - relaunching elevated (one UAC)..."
  try {
    Start-Process powershell -Verb RunAs -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $selfPath) -ErrorAction Stop
  } catch {
    Write-Host "Elevation cancelled: $($_.Exception.Message)"
  }
  exit
}

$vm = $null
try { $vm = Get-VM -Name $vmName -ErrorAction Stop } catch { }
if (-not $vm) {
  Write-Host "VM '$vmName' not found. Create it first (see windows-admin skill, hyperv-dirty-box)."
  Read-Host "Press Enter to close"
  exit
}

if ($vm.State -ne 'Running') {
  Write-Host "Starting VM $vmName (state was $($vm.State))..."
  try { Start-VM -Name $vmName -ErrorAction Stop; Start-Sleep -Seconds 4 } catch { Write-Host "Start-VM failed: $($_.Exception.Message)" }
}

# Opening vmconnect again either creates the console or raises the existing window.
Write-Host "Opening console for $vmName..."
Start-Process $vmconnect -ArgumentList 'localhost', $vmName
