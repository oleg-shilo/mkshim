$packageName = 'MkShim'
$url = 'https://github.com/oleg-shilo/mkshim/releases/download/v1.2.0.0/mkshim.zip'

Stop-Process -Name "mkshim"

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$checksum = '38F059639BCB301521B41807B831F0C7E045D8DCC37E1EFDAEA611635CCE610B'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType
