# Clarity · Windows C-drive cleaner

[简体中文](README.md) | **English**

**Clarity (澄净)** is a portable Windows cleanup utility by **CraZyX**. It finds system junk and application caches through built-in rules, application fingerprints, the Winapp2 community database, and bounded heuristic scanning. It offers simple cleanup for eligible low-risk files and an advanced mode with individual selection, explanations, and risk confirmation.

[Download beta0.10](https://github.com/CrazyHandin/Clarity/releases/tag/beta0.10) · [All releases](https://github.com/CrazyHandin/Clarity/releases) · [Version history](CHANGELOG.md)

## Features

- **Manual start, quick by default.** Opening the app does not start a scan. Quick scan uses only built-in rules eligible for simple cleanup and skips application discovery and external rules. Regular scan provides the full scan and advanced selection.
- **Clear selection and explanations.** Results are grouped by category, application, and cleanup item. Search, filters, group selection, and separate risk labels help you review the plan. Unknown caches appear under Software caches → Application → Unknown caches.
- **Common application support.** The beta0.10 built-in catalog contains 157 rules, including Windows, WeChat, QQ, browsers, Adobe applications, VS Code, WPS, music players, and development tools. Of these, 28 rules can participate in quick scanning; eligibility still depends on file age, running applications, and safety checks.
- **Application discovery.** Regular scan reads uninstall entries, App Paths, Start menu shortcuts, processes, and service information, with executable version and signature evidence. You can add portable application folders and open a discovered application's file location.
- **Winapp2 and custom rules.** Import, update, export, or roll back Winapp2 INI rules. Signed JSON rules remain an independent option with a separately saved HTTPS address. Community and heuristic results are advanced-only and require backups.
- **Backup and restore.** File cleanup defaults to making a recoverable backup first. Open backup locations from the interface. Successfully restored files are verified and their corresponding backup copies are removed; unresolved files remain available for another attempt.
- **Optional Windows Disk Cleanup.** A separate, unchecked option runs the Windows tool for C: and waits for it to finish. The completion screen reports cleanup results and returns to the preparation screen when you click Finish.

## Get started

1. Download `Clarity-beta0.10-portable.zip` from the release page and extract it.
2. Run `Clarity.exe`; keep `Clarity.exe.config` beside it. The application interface is currently in Simplified Chinese.
3. Use the default quick scan, or choose regular scan for application discovery, community rules, unknown caches, and advanced cleanup. Click **开始扫描** (Start scan).
4. Review the results and the planned amount. Simple cleanup includes eligible low-risk items. Advanced cleanup lets you choose individual files and risk items.
5. Click the cleanup button. Confirmation appears when the selected plan includes risk items. Review the completion screen, then click **完成** (Finish) to return to the initial screen.

Windows 10/11 with **.NET Framework 4.8** is required. No installation, Python, Node.js, or .NET SDK is needed. The executable is AnyCPU; validation was performed on Windows x64. System folders may require administrator access. Releases through beta0.08 use the historical executable name `ClearSpace.exe`; beta0.09 and later use `Clarity.exe`.

## Risk and backups

Low-risk caches can be rebuilt, but removing them may slow the next launch or require downloads. Diagnostics, offline resources, media caches, usage traces, registry entries, community rules, and unknown caches are presented separately for review. Advanced risk deletion can include normally retained personal or recent files; read each item's explanation before selecting it. Core system protections and explicit exclusions remain enforced.

The default file backup folder is **ClarityBackups** on a suitable local fixed drive other than C:, for example `D:\ClarityBackups`. If none is available, the fallback is `%LOCALAPPDATA%\ClearSpace\ClarityBackups`. You can change it in the rules and recovery window. Existing backups remain at their previous location and can still be selected manually.

Restoration uses the batch's `files.csbackup` index and requires the same Windows computer and account. Existing destination files are not overwritten. A backup on C: does not free the original file's space until that backup is removed. The displayed release amount is an estimate, with same-drive backups excluded from estimated net savings. Locked, changed, linked, read-only, or inaccessible files are skipped.

Windows Disk Cleanup runs `cleanmgr.exe /d C /verylowdisk` with Windows' default items. Its deletions bypass Clarity's backups and may be permanent; Windows calculates its own size, which is excluded from Clarity's estimates. The option is disabled by default.

## Rule updates and privacy

The bundled Winapp2 snapshot is version **260915**, with 4,068 original entries. The adapter supports a restricted subset of file operations. Community rules are not automatically selected, do not enter simple cleanup, and always require a backup. Arbitrary community registry deletion is not executed. A rule count does not mean that every listed application or cache is present on your computer.

Winapp2 updates use the project's fixed [HTTPS INI address](https://raw.githubusercontent.com/MoscaDotTo/Winapp2/master/Winapp2.ini). Local INI import works offline. Optional periodic update checks are off by default. The JSON update URL starts empty and is saved independently; cloud JSON updates require the bundled publisher's signature.

Updates fetch rule files without uploading application inventories, file paths, or scan results. Updating does not trigger scanning or deletion. Settings, cached rules, logs, and registry backups use `%LOCALAPPDATA%\ClearSpace`; this existing data folder is retained for compatibility.

Winapp2 rules are distributed unchanged under **CC BY-SA 4.0**. See [third-party notices](THIRD_PARTY_NOTICES.md), the [included license](Winapp2-License.md), and the [upstream project](https://github.com/MoscaDotTo/Winapp2). Clarity's executable is not code-signed. Publisher signatures for JSON rules authenticate rule packages, not the executable. Release assets include SHA-256 checksums.

## Build from source

The application is written in C# and WPF. The build script uses the .NET Framework compiler installed with Windows and reads the release number from `version.json`.

```powershell
.\build.ps1 -OutputDirectory .\dist
```

Run the isolated checks with the built executable:

```powershell
.\dist\Clarity.exe --self-test .\verification\files
.\dist\Clarity.exe --v2-self-test .\verification\registry
.\dist\Clarity.exe --v3-self-test .\verification\rules
.\dist\Clarity.exe --winapp2-self-test .\verification\winapp2
.\dist\Clarity.exe --beta-self-test .\verification\beta
.\dist\Clarity.exe --recovery-self-test .\verification\recovery
.\dist\Clarity.exe --ui-self-test .\verification\ui
.\dist\Clarity.exe --common-rules-self-test .\verification\common
```

The beta0.10 executable passed **274 checks** across these eight suites. Deletion checks use generated fixtures and randomly named HKCU test branches; they do not clean real caches. Windows Disk Cleanup is tested through a simulated runner. Historical versions have their own verification results.

The repository imports ten archived source snapshots as separate commits and tags, **beta0.01–beta0.10**. This preserves each release's source state; it is not an original development commit log. Chinese technical documentation is included for cleanup boundaries, common application rules, rule maintenance, and recovery.

**by CraZyX**
