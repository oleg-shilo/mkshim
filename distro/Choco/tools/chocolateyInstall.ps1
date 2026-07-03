$packageName = 'MkShim'
$url = 'https://github.com/oleg-shilo/mkshim/releases/download/v1.6.0.0/mkshim.zip'

Stop-Process -Name "mkshim"

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$checksum = '03F4889E20C9C0AFABA83DF63C65383E66ECF7A4CFF9F5A06F8F33D510084223'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType
