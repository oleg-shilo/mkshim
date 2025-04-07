echo off
echo *******************
echo *  must be admin  *
echo *******************

choco pack
REM choco install mkshim -s '%cd%' --force