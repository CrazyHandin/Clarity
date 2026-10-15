# Clarity

[Chinese](README.md) | **English**

Clarity is a Windows C-drive cleaner available as an installer or portable app, with system junk and application cache detection, simple cleanup for eligible low-risk items, advanced selection, and backup restoration.

[Download latest](https://github.com/CrazyHandin/Clarity/releases/latest) · [All releases](https://github.com/CrazyHandin/Clarity/releases) · [Changelog](CHANGELOG.md)

## Features

- **Common application support:** Cache and log rules for Windows, WeChat, QQ, browsers, Adobe, VS Code, WPS, and more.
- **Clear cleanup choices:** Results grouped by category and application, with search, filters, group selection, and explanations of each item's purpose, impact, and risk.
- **Application and rule discovery:** Application fingerprints, built-in rules, Winapp2, and unknown-cache detection, with portable application folders, rule updates, and offline import.
- **Backup and restore:** Backup-first cleanup, shortcuts to backup locations, and automatic removal of backup copies after verified restoration.
- **History and updates:** Automatically saved cleanup records with a direct shortcut in Settings, startup update checks, and online upgrades for installed and portable editions.

## Get started

Requires **Windows 10/11 and .NET Framework 4.8**. Choose English or Simplified Chinese under **Settings & Backups → General**. The included TXT guide contains Chinese instructions followed by the English guide.

1. Run the installer, or extract the portable package and run `Clarity.exe` with `Clarity.exe.config` beside it.
2. Choose a scan mode and click **Start scan**. Opening the app does not start scanning.
3. Review the results and clean your selection. Risk items trigger confirmation. Click **Done** on the completion screen to return to the initial screen.

| Scan mode | Scope | Cleanup options |
| --- | --- | --- |
| Quick (default) | Built-in rules eligible for simple cleanup; skips complex discovery | Simple cleanup |
| Regular | Built-in rules, application discovery, external rules, and unknown caches | Simple / Advanced |

Administrator restart is under **Settings & Backups → General** and is also offered after failed or access-denied cleanup. Restarting does not automatically scan or continue deletion. Credits are in **About Clarity**.

Settings, About, and the built-in user guide open inside the main window. Returning to cleanup preserves scan results and selections. About contains version information, a Beta notice, and third-party attribution.

Click **Check for software updates** in About. Available updates open in a small window with release notes and download progress. New versions add red badges to Settings and About. **What's new in this version** shows the current version's summary.

Simple cleanup handles only eligible low-risk items. Advanced cleanup allows individual selection and review. Unknown caches and community rules are unchecked by default, advanced-only, and always backed up.

## Backups and risks

The default backup folder is **ClarityBackups**, preferably on another local drive. Change it under Settings & Backups → Backup & Restore. Backups stored on C: must be removed before their corresponding space is freed.

To restore, select the batch's `files.csbackup` index. Restoration requires the same computer and Windows account and does not overwrite existing files. Verified restored copies are removed from the backup; conflicts and failed items remain available.

Removing low-risk caches may require downloads or slow the next launch. Advanced risk deletion can affect personal data; review item explanations. Files that are in use, inaccessible, or changed after scanning are skipped.

**Windows Disk Cleanup** is a separate option, disabled by default. It automatically processes C: and exits when finished. Its deletions bypass Clarity's backups and may be irreversible.

## Rules and privacy

Update or import Winapp2 INI and JSON rules under Settings & Backups → Rule Management. Updates fetch rule files without uploading application inventories, paths, or scan results, and do not automatically scan or clean.

The Winapp2 database is distributed under **CC BY-SA 4.0**. See [third-party notices](THIRD_PARTY_NOTICES.md).

## Build and documentation

Build the C# / WPF application with the .NET Framework compiler:

```powershell
.\build.ps1 -OutputDirectory .\dist
```

Source folders: `src/` (UI, cleaning, rules, discovery, recovery), `tests/` (isolated checks), `assets/` (icons and configuration), `rules/` (rule data), `installer/` (setup configuration), `scripts/` (tools), and `docs/` (technical documentation). See [build and release instructions](docs/BUILD.md) for installer packaging and publishing.

Technical documentation (Chinese): [Application rules](docs/%E5%B8%B8%E7%94%A8%E8%BD%AF%E4%BB%B6%E8%A7%84%E5%88%99.md) · [Cleanup catalog](docs/%E6%B8%85%E7%90%86%E8%A7%84%E5%88%99%E4%B8%80%E8%A7%88.md) · [Rule maintenance](docs/%E8%A7%84%E5%88%99%E5%BA%93%E7%BB%B4%E6%8A%A4.md) · [Winapp2 guide](docs/Winapp2-%E4%BD%BF%E7%94%A8%E8%AF%B4%E6%98%8E.md)

## License

Clarity's own code, application, and documentation use the [MIT license](LICENSE.txt), allowing redistribution and commercial use under its conditions. Winapp2 rules retain their separate CC BY-SA 4.0 license.

[Terms and cleanup notes](docs/TERMS.txt) are available from About, covering scanning, backups, restoration, and cleanup risks.
