# mkshim

[Install from winget](https://winstall.app/apps/oleg-shilo.mkshim):    
```
winget install --id=oleg-shilo.mkshim  -e
``` 
---

This is a simple application for creating shims on Windows. 

The problem it is trying to solve is a lack of good technical solutions for executable aliases on Windows. While on linux the thangs are much simpler, on Windows synbolic links are not as efficient as they do not handle well executables that have dependencies.

Chocolatey (the Package Manager for Windows) has solved this problem by implementing its own shim generation tool [ShimGen.exe](https://docs.chocolatey.org/en-us/features/shim). However this tool is not available unless the target system has Chocolatey installed.

MkShim is a simple alternative tool that delivers the same functionality as ShimGen and is distributed independently and under MIT licence.

CLI help:

```txt
Usage:
   mkshim <shim_name> <target_executable> [--params=<args>]

-v | --version
    Prints mkshim version.
-p:args | --params:args
    The defaul arguments you always want to pass to the target executable.
    IE with chrome.exe shim: 'chrome.exe --save-page-as-mhtml --user-data-dir="/some/path"'

You can use special mkshim arguments with the created shim:
 --mkshim-noop
   Execute created shim but print <target_executable> instead of executing it.
 --mkshim-test
   Tests if shim's <target_executable> exists.
``` 

Thus if you want to create a shim `ntp` for launching `notepad.exe`, then you can achieve this by simply executing the following command from the terminal:
```
mkshim C:\ProgramData\chocolatey\bin\ntp.exe C:\Windows\System32\notepad.exe
```
Note: in this example, the shim is created in the Cocolatey bin folder, which is in the system PATH.

---
When it comes to working with the shims you created, it is always useful to be able to extract the information about shim's target file details (e.g. path).
You can do this by executing the shim with the special mkshim-specific command line parameters: 
_IE rc.exe shim is created for the Windows SDK resource compiler rc.exe_

- `--mkshim-noop` prints shim mapping info
  ```txt
  D:\tools\>rc.exe --mkshim-noop
  Executing shim in 'no operation' mode.
  Target: C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\rc.exe
  Default params: 
  ```
- `--mkshim-test` prints shim mapping info
  ```txt
  D:\tools\>rc.exe --mkshim-test
  Success: target file exists.
  Target: C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\rc.exe
  ```
However, the above options are unavailable if your shim is a Windows executable, not a console application. In such cases, you can use rather excellent PE metadata reader tool from Sysinternals [Sigcheck]([url](https://winstall.app/apps/Microsoft.Sysinternals.Sigcheck)):

```txt
D:\tools>sigcheck rc.exe

Sigcheck v2.90 - File version and signature viewer
Copyright (C) 2004-2022 Mark Russinovich
Sysinternals - www.sysinternals.com

d:\tools\rc.exe:
        Verified:       Unsigned
        Link date:      1:11 PM 22/03/2025
        Publisher:      n/a
        Company:        n/a
        Description:    Shim to rc.exe (created with mkshim v1.1.0.0); Default params: 
        Product:        C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\rc.exe
        Prod version:   10.0.22621.3233
        File version:   10.0.22621.3233 (WinBuild.160101.0800)
        MachineType:    64-bit
```
