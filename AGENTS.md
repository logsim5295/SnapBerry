# Repository Instructions

- Preserve the normal development Release build.
- Do not launch SnapBerry or run tests that show windows, activate applications, capture the desktop, or steal focus without explicit user approval. Prefer headless tests and builds, and report interactive tests that were not run.
- After application changes, run the relevant tests, build the normal Release, and publish the self-contained single EXE using:
  `dotnet publish src/SnapBerry.App/SnapBerry.App.csproj -c Release -p:PublishProfile=Portable`
- Distribute Windows EXE/ZIP and macOS DMG installers only through GitHub Releases, using the version-tag Release workflow. Keep generated installers local; do not commit EXE, DMG, or generated installer archives to Git or Git LFS. Application changes still require the tests, normal Release build, and local Portable publish specified above.
- For every new version release, summarize the changes since the previous release in Korean in the GitHub release notes. Include user-facing features, improvements, and fixes; distinguish platform-specific changes, preserve download and installation instructions, and verify that the published release notes contain the summary.
- Do not overwrite a running executable. Ask the user to exit SnapBerry through the tray menu when necessary.
- Write commit subjects and bodies in Korean. Use author name `ynsim` and email `logsim1234@gmail.com`.
- Do not push without explicit user authorization.
