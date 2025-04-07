$packageName = 'GC-Menu'

$FileName = [IO.Path]::Combine([Environment]::GetFolderPath('Startup'), 'GlobalContextMenu.lnk')
if (Test-Path $FileName) {
  Remove-Item $FileName
}

# Get-Process notepad | Stop-Process
Stop-Process -Name "gc-menu"
