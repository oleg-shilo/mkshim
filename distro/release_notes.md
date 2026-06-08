# Release v1.5.2.0

- #7: Improved environment variable support in target path
  - Removed `--keep-envars` option; env-var expansion is now inferred automatically from the `{env:VAR_NAME}` syntax in the target path
  - Added support for escaped braces (`}}`) in target paths containing a literal `}` character
- #8: Fixed crash when generating a shim for executables with certain icon formats (e.g. qutebrowser)
- Added CLI argument alias mapping via optional `mkshim.cli-map` file placed next to `mkshim.exe`
- Added warnings for unrecognised CLI switches
- Improved `--mkshim-noop` output: resolved target path is now displayed correctly when env-vars are present