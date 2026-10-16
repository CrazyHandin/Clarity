# Building and publishing Clarity

Use Windows 10/11, .NET Framework 4.8, and PowerShell 5.1 or newer. Installer builds require [Inno Setup 6.7.3 or newer](https://jrsoftware.org/isdl.php). No compiler downloads are performed by the build scripts.

```powershell
$version = .\scripts\Get-ReleaseVersion.ps1
$tag = $version.Tag
.\build.ps1 -OutputDirectory ".\releases\Clarity-$tag" -WithTests
.\scripts\verify-release.ps1 -Executable ".\releases\Clarity-$tag\Clarity.exe" -OutputDirectory .\release-verification
.\package-release.ps1 -VerificationDirectory .\release-verification -InstallerCompiler 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

The default build contains only production source. `-WithTests` also builds a separate `_verification/Clarity.Tests.exe` host with the same source and application resources. Functional suites operate on generated fixtures and temporary test registry keys in that host. A separate suite inspects the production assembly's metadata and resources without executing it, checking that test types, diagnostic launchers and test-only registry permissions are absent. Tests do not clean personal caches or run Windows Disk Cleanup.

`build-manifest.json` records the source/resource hashes and both executable hashes. Verification and packaging reject changed source inputs or mismatched binaries. Packaging then produces the portable ZIP, source ZIP, installer EXE, a build-specific checksum file, and `Clarity-update.json`. The test host and build manifest are not distributed in the portable or installer packages.

The Winapp2 database is copied to `rules/Winapp2.ini` beside each build rather than embedded in an executable. Its expected SHA-256 is compiled into the application, recorded in the build manifest, and checked before loading the bundled snapshot. Keep the rules folder when moving a portable build. Explicit rule imports and downloads continue to use the separate application cache.

The installer requests administrator permission and defaults to the native Program Files folder (normally `C:\Program Files\Clarity`, including on 64-bit Windows). The destination page remains available during upgrades. Browse opens the Windows folder picker with access to Desktop, Downloads, This PC, and other shell locations, then appends a Clarity subfolder if needed. It supports upgrades and uninstall and offers an optional desktop shortcut. It installs the application, runtime configuration, bundled rule data, and one TXT user guide. It checks for .NET Framework 4.8. Uninstall removes installed program files and shortcuts; settings, backups, and unrelated files are preserved.

The source archive includes only listed source, tests, resources, rules, scripts, installer configuration, and documentation. Private signing keys, account credentials, real scan reports, backup files, and build output are excluded.

After reviewing and committing the changes, push `main` and a tag matching `version.json`. With the [GitHub CLI](https://cli.github.com/) authenticated and a short release-notes file prepared:

```powershell
.\scripts\publish-release.ps1 -NotesFile .\release-notes.md
```

`version.json` defines the release channel, version, and internal `build` number, starting at 1. Stable releases use `channel: "stable"` and a three-part `version` such as `1.0.0`; historical beta releases use `channel: "beta"` and `number`. Stable Windows file versions use `major.minor.patch.build`, while the public version and About page use `major.minor.patch` with the internal build shown separately. Packages include the build in their names, such as `Clarity-1.0.0-build1-setup.exe`; their contents cannot be replaced under the same name. Keep each build's source commit tagged for reproducibility.

Publishing creates the release if necessary, uploads and verifies the packages and build-specific checksum file, then updates `Clarity-update.json` to point to that build. Once the new build is verified and marked **Latest**, older build packages and checksum attachments are removed from that release. Source commit tags remain available. The GitHub prerelease switch stays off. The updater reads the manifest from the official release's HTTPS download URL, validates version, names and sizes, and verifies package SHA-256 hashes before applying an update. It does not depend on the anonymous REST API quota. Bilingual update summaries are set in `version.json` and included in the manifest and application.
