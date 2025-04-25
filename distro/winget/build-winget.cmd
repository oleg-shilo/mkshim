echo off


cd .\..\..\..\winget-pkgs


echo Sync the https://github.com/oleg-shilo/winget-pkgs fork with the MS upstream repo...

pause

git remote add upstream https://github.com/microsoft/winget-pkgs
git fetch upstream
git checkout master
git reset --hard upstream/master  
git push origin master --force
rem git checkout master
rem git pull

cd ..\mkshim\distro\winget
css -c:0 -ng:csc .\update_winget_scripts

