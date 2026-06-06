$packageName = 'MkShim'
$url = 'https://github.com/oleg-shilo/mkshim/releases/download/v1.5.1.0/mkshim.zip'

Stop-Process -Name "mkshim"

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$checksum = 'B30D69DA666A3F1FFE59765B3341BD6967CFA7882CC28D1485B7741C52C49BA4'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType
