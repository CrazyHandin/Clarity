# Building and publishing Clarity

Use Windows 10/11, .NET Framework 4.8, and PowerShell 5.1 or newer. Installer builds require [Inno Setup 6.7.3 or newer](https://jrsoftware.org/isdl.php). No compiler downloads are performed by the build scripts.

```powershell
$version = Get-Content .\version.json -Raw | ConvertFrom-Json
$tag = 'beta0.{0:00}' -f $version.number
.\build.ps1 -OutputDirectory ".\releases\Clarity-$tag"
.\scripts\verify-release.ps1 -Executable ".\releases\Clarity-$tag\Clarity.exe" -OutputDirectory .\release-verification
.\package-release.ps1 -VerificationDirectory .\release-verification -InstallerCompiler 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

The verification suites operate on generated fixtures and temporary test registry keys. They do not clean personal caches or run Windows Disk Cleanup. Packaging checks that the reports belong to the exact executable being packaged, then produces the portable ZIP, source ZIP, installer EXE, a build-specific checksum file, and `Clarity-update.json`.

The installer requests administrator permission and defaults to the native Program Files folder (normally `C:\Program Files\Clarity`, including on 64-bit Windows). The destination page remains available during upgrades. Browse opens the Windows folder picker with access to Desktop, Downloads, This PC, and other shell locations, then appends a Clarity subfolder if needed. It supports upgrades and uninstall and offers an optional desktop shortcut. It installs the application, runtime configuration, and one TXT user guide. It checks for .NET Framework 4.8. Uninstall removes installed program files and shortcuts; settings, backups, and unrelated files are preserved.

The source archive includes only listed source, tests, resources, rules, scripts, installer configuration, and documentation. Private signing keys, account credentials, real scan reports, backup files, and build output are excluded.

After reviewing and committing the changes, push `main` and a tag matching `version.json`. With the [GitHub CLI](https://cli.github.com/) authenticated and a short release-notes file prepared:

```powershell
.\scripts\publish-release.ps1 -NotesFile .\release-notes.md
```

`version.json` defines the beta version and its internal `build` number, starting at 1. Small updates increment `build` within the same beta release. Packages include the build in their names, such as `Clarity-beta0.20-build1-setup.exe`; their contents cannot be replaced under the same name. Keep each build's source commit tagged for reproducibility.

Publishing creates the beta release if necessary, uploads and verifies the packages and build-specific checksum file, then updates `Clarity-update.json` to point to that build. Older build attachments remain available in the same release. The release is marked GitHub **Latest**, with the GitHub prerelease switch off. The updater checks the manifest and GitHub asset digests before applying an update.
