# mkshim

This is a simple application for creating shims on Windows. 

The problem it is trying to solve is a lack of good technical solutions for executable aliases on Windows. While on linux the thangs are much simpler, on Windows synbolic links are not as efficient as they do not handle well executables that have dependencies.

Chocolatey (the Package Manager for Windows) has solved this problem by implementing its own shim generation tool [ShimGen.exe](https://docs.chocolatey.org/en-us/features/shim). However this tool is not available unless the target system has Chocolatey installed.

MkShim is a simple alternative tool that delivers the same functionality as ShimGen and is distributed independently and under MIT licence.

Usage:
```txt
mkshim <shim_name> <mapped_executable>
``` 

Thus if you want to create a shim `ntp` for launching `notepad.exe`, then you can achieve this by simply executing the following command from the terminal:
```
mkshim C:\ProgramData\chocolatey\bin\ntp.exe C:\Windows\System32\notepad.exe
```
Note: in this example, the shim is created in the Cocolatey bi folder, which is in the system PATH.
