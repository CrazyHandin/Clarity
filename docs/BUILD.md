# Building and publishing Clarity

Use Windows 10/11, .NET Framework 4.8, and PowerShell 5.1 or newer. Installer builds require [Inno Setup 6.7.3 or newer](https://jrsoftware.org/isdl.php). No compiler downloads are performed by the build scripts.

```powershell
$version = Get-Content .\version.json -Raw | ConvertFrom-Json
$tag = 'beta0.{0:00}' -f $version.number
.\build.ps1 -OutputDirectory ".\releases\Clarity-$tag"
.\scripts\verify-release.ps1 -Executable ".\releases\Clarity-$tag\Clarity.exe" -OutputDirectory .\release-verification
.\package-release.ps1 -VerificationDirectory .\release-verification -InstallerCompiler 'C:\Program Files\Inno Setup 7\ISCC.exe'
```

The verification suites operate on generated fixtures and temporary test registry keys. They do not clean personal caches or run Windows Disk Cleanup. Packaging checks that the reports belong to the exact executable being packaged, then produces the portable ZIP, source ZIP, installer EXE, and `SHA256SUMS.txt`.

The installer uses the current Windows account, supports upgrades and uninstall, and offers an optional desktop shortcut. It installs the application, runtime configuration, and one TXT user guide. It checks for .NET Framework 4.8. Uninstall removes installed program files and shortcuts; settings, backups, and unrelated files are preserved.

The source archive includes only listed source, tests, resources, rules, scripts, installer configuration, and documentation. Private signing keys, account credentials, real scan reports, backup files, and build output are excluded.

After reviewing and committing the changes, push `main` and a tag matching `version.json`. With the [GitHub CLI](https://cli.github.com/) authenticated and a short release-notes file prepared:

```powershell
.\scripts\publish-release.ps1 -NotesFile .\release-notes.md
```

Publishing creates a draft, uploads and verifies the four attachments, then marks the release as GitHub **Latest**. It refuses to replace an existing version. The version label can contain `beta`, but the GitHub prerelease switch remains off so Latest is available. Historical attachments are preserved.
