# Release v1.7.0.0

- #10: Feature Suggestion: Alternative preferred executable
- Added `--target` CLI option to support multiple targets
  CLI `--target` is also parsing to support conditional suffixes (e.g., `?elevated`) and preserve both build-time and runtime path forms per target. The conditional suffixes are a preview feature and may be changed in the next release.

```ps
  --target:<target_executable>
    Path to the target executable to be pointed to by the created shim.
    You can specify multiple targets. In such case the first target found will be executed at runtime:
       mkshim vs "--target:\IDE Professional\ide.exe" "--target:\IDE Community\ide.exe"
```