$packageName = 'MkShim'
$url = 'https://github.com/oleg-shilo/mkshim/releases/download/v1.1.5.0/mkshim.zip'

Stop-Process -Name "mkshim"

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$checksum = '6B400F70FD37C4422C6F4F484E5D81118407E17C11D6D0CC2386006669064521'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType
