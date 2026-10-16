# Building and publishing Clarity

Use Windows 10/11, .NET Framework 4.8, and PowerShell 5.1 or newer. Installer builds require [Inno Setup 6.7.3 or newer](https://jrsoftware.org/isdl.php). No compiler downloads are performed by the build scripts.

```powershell
$version = .\scripts\Get-ReleaseVersion.ps1
$tag = $version.Tag
.\build.ps1 -OutputDirectory ".\releases\Clarity-$tag" -WithTests
.\scripts\verify-release.ps1 -Executable ".\releases\Clarity-$tag\Clarity.exe" -OutputDirectory .\release-verification
.\package-release.ps1 -VerificationDirectory .\release-verification -InstallerCompiler 'C:\Program Files\Inno Setup 7\ISCC.exe' -UpdateSigningKey 'D:\Private\software-update-key.xml'
```

The default build contains only production source. `-WithTests` also builds a separate `_verification/Clarity.Tests.exe` host with the same source and application resources. Functional suites operate on generated fixtures and temporary test registry keys in that host. A separate suite inspects the production assembly's metadata and resources without executing it, checking that test types, diagnostic launchers and test-only registry permissions are absent. Tests do not clean personal caches or run Windows Disk Cleanup.

`build-manifest.json` records the source/resource hashes and both executable hashes. Verification and packaging reject changed source inputs or mismatched binaries. Packaging then produces the portable ZIP, source ZIP, installer EXE, a checksum file, and `Clarity-update.json`. The test host and build manifest are not distributed in the portable or installer packages.

The Winapp2 database is copied to `rules/Winapp2.ini` beside each build rather than embedded in an executable. Its expected SHA-256 is compiled into the application, recorded in the build manifest, and checked before loading the bundled snapshot. Keep the rules folder when moving a portable build. Explicit rule imports and downloads continue to use the separate application cache.

The installer requests administrator permission and defaults to the native Program Files folder (normally `C:\Program Files\Clarity`, including on 64-bit Windows). The destination page remains available during upgrades. Browse opens the Windows folder picker with access to Desktop, Downloads, This PC, and other shell locations, then appends a Clarity subfolder if needed. It supports upgrades and uninstall and offers an optional desktop shortcut. It installs the application, runtime configuration, bundled rule data, and one TXT user guide. It checks for .NET Framework 4.8. Uninstall removes installed program files and shortcuts; settings, backups, and unrelated files are preserved.

The source archive includes only listed source, tests, resources, rules, scripts, installer configuration, and documentation. Private signing keys, account credentials, real scan reports, backup files, and build output are excluded.

`Clarity.UpdateService.exe` is built separately from `src/UpdateService` and depends on the installed Clarity assembly. Setup installs and registers the service for protected Program Files installations. It is demand-started, accepts requests from local interactive clients, checks the client's process identity, downloads only the official signed update, then exits. A protected staging runner lets setup stop and replace the service itself; a separate user-session helper restarts Clarity without launching the UI as SYSTEM. Uninstall stops and removes the service.

Writable custom installations use a verified ZIP and the current user's transactional file updater. A DPAPI-protected plan binds the account, machine, parent process, target and signed package. The helper rechecks the official release before waiting for the parent to exit. Installed copies select only the six setup payload files; uninstall records, logs, backups and portable-only documents are excluded. Program Files never uses this custom-location route. Unwritable installations, or Program Files installations without a valid service, retain the authorized installer route. File updates preserve existing uninstall registration; an all-users Windows Apps display version can remain at its last installer version until setup runs again.

`assets/update-public-key.xml` pins the RSA-3072 update publishing key. Packaging signs the update manifest with a matching private key supplied through `-UpdateSigningKey`. This key is distinct from rule-pack signing keys and must remain outside the repository and archives. `SignedPayload` and `Signature` authenticate the same version, hashes and package records exposed to older clients. Signing verifies the key pair before creating a release; the updater checks the publisher signature before accepting network manifests.

After reviewing and committing the changes, push `main` and a tag matching `version.json`. With the [GitHub CLI](https://cli.github.com/) authenticated and a short release-notes file prepared:

```powershell
.\scripts\publish-release.ps1 -NotesFile .\release-notes.md
```

`version.json` defines the release channel, version, and internal `build` number, starting at 1. Stable releases use `channel: "stable"` and a three-part `version` such as `1.0.0`; historical beta releases use `channel: "beta"` and `number`. Stable Windows file versions use `major.minor.patch.build`, while the public version and About page use `major.minor.patch` with the internal build shown separately. Packages use the public version in their names, such as `Clarity-1.0.6-setup.exe`. The update manifest and executable metadata retain the internal build number. Keep each build's source commit tagged for reproducibility.

Publishing creates the release if necessary, uploads and verifies the packages and checksum file, then updates `Clarity-update.json` to point to that build. Once the new build is verified and marked **Latest**, older build packages and checksum attachments are removed from that release. Source commit tags remain available. The GitHub prerelease switch stays off. The updater reads the manifest from the official release's HTTPS download URL, validates version, names and sizes, and verifies package SHA-256 hashes before applying an update. It does not depend on the anonymous REST API quota. Bilingual update summaries are set in `version.json` and included in the manifest and application.
