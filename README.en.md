# Clarity (澄净)

[简体中文](README.md) | **English**

Clarity is a portable Windows C-drive cleaner with system junk and application cache detection, simple cleanup for eligible low-risk items, advanced selection, and backup restoration. **by CraZyX**

[Download](https://github.com/CrazyHandin/Clarity/releases/tag/beta0.10) · [All releases](https://github.com/CrazyHandin/Clarity/releases) · [Changelog](CHANGELOG.md)

## Features

- **Common application support:** Cache and log rules for Windows, WeChat, QQ, browsers, Adobe, VS Code, WPS, and more.
- **Clear cleanup choices:** Results grouped by category and application, with search, filters, group selection, and explanations of each item's purpose, impact, and risk.
- **Application and rule discovery:** Application fingerprints, built-in rules, Winapp2, and unknown-cache detection, with portable application folders, rule updates, and offline import.
- **Backup and restore:** Backup-first cleanup, shortcuts to backup locations, and automatic removal of backup copies after verified restoration.

## Get started

Requires **Windows 10/11 and .NET Framework 4.8**. No installation is needed. The application interface is in Simplified Chinese.

1. Download and extract the portable package. Run `Clarity.exe` with `Clarity.exe.config` beside it.
2. Choose a scan mode and click **开始扫描** (Start scan). Opening the app does not start scanning.
3. Review the results and clean your selection. Risk items trigger confirmation. Click **完成** (Finish) on the results screen to return to the initial screen.

| Scan mode | Scope | Cleanup options |
| --- | --- | --- |
| Quick (default) | Built-in rules eligible for simple cleanup; skips complex discovery | Simple cleanup |
| Regular | Built-in rules, application discovery, external rules, and unknown caches | Simple / Advanced |

Simple cleanup handles only eligible low-risk items. Advanced cleanup allows individual selection and review. Unknown caches and community rules are unchecked by default, advanced-only, and always backed up.

## Backups and risks

The default backup folder is **ClarityBackups**, preferably on another local drive. Change it in the rules and recovery window. Backups stored on C: must be removed before their corresponding space is freed.

To restore, select the batch's `files.csbackup` index. Restoration requires the same computer and Windows account and does not overwrite existing files. Verified restored copies are removed from the backup; conflicts and failed items remain available.

Removing low-risk caches may require downloads or slow the next launch. Advanced risk deletion can affect personal data; review item explanations. Files that are in use, inaccessible, or changed after scanning are skipped.

**Windows Disk Cleanup** is a separate option, disabled by default. It automatically processes C: and exits when finished. Its deletions bypass Clarity's backups and may be irreversible.

## Rules and privacy

Update or import Winapp2 INI and JSON rules in the application and rules window. Updates fetch rule files without uploading application inventories, paths, or scan results, and do not automatically scan or clean.

The Winapp2 database is distributed under **CC BY-SA 4.0**. See [third-party notices](THIRD_PARTY_NOTICES.md).

## Build and documentation

Build the C# / WPF application with the .NET Framework compiler:

```powershell
.\build.ps1 -OutputDirectory .\dist
```

Technical documentation (Chinese): [Application rules](常用软件规则.md) · [Cleanup catalog](清理规则一览.md) · [Rule maintenance](规则库维护.md) · [Winapp2 guide](Winapp2-使用说明.md)
