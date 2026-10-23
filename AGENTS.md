# Repository Instructions

- Preserve the normal development Release build.
- Do not launch SnapBerry or run tests that show windows, activate applications, capture the desktop, or steal focus without explicit user approval. Prefer headless tests and builds, and report interactive tests that were not run.
- After application changes, run the relevant tests, build the normal Release, and publish the self-contained single EXE using:
  `dotnet publish src/SnapBerry.App/SnapBerry.App.csproj -c Release -p:PublishProfile=Portable`
- Keep `dist/portable-win-x64/SnapBerry.exe` updated with application changes and include it in the corresponding commit. This binary is tracked by Git LFS; do not commit it as a regular Git blob.
- Do not overwrite a running executable. Ask the user to exit SnapBerry through the tray menu when necessary.
- Write commit subjects and bodies in Korean. Use author name `ynsim` and email `s921119@gmail.com`.
- Do not push without explicit user authorization.
