# Release v1.7.0.0-1.7.1.0

- #10: Feature Suggestion: Alternative preferred executable
- Added new primary parameters constraint. Now both shim name and target names must be specified either implicitly (without prefix) or with both `--shim:` and `--target:` prefixes. This is to avoid ambiguity when both forms are mixed.
- Added optional `--shim` CLI parameter prefix to specify shim name
- Added `--target` CLI parameter as an option to support multiple targets
  CLI `--target` is also parsing to support conditional suffixes (e.g., `?elevated`) and preserve both build-time and runtime path forms per target. The conditional suffixes are a preview feature and may be changed in the next release.

```ps
  --shim:<shim_executable>
    Path to the shim executable to be created. If `--shim:` is not specified, the first positional
    argument will be used as the shim executable.
    If you use `--shim:` then you also need to specify targets with the explicit `--target:` prefix.

  --target:<target_executable>
    Path to the target executable to be pointed to by the created shim.
    You can specify multiple targets. In such case the first target found will be executed at runtime:
       mkshim --shim:vs "--target:\IDE Professional\ide.exe" "--target:\IDE Community\ide.exe"

```