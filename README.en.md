# Clarity (澄净) beta0.05

[简体中文](README.md) | **English**

Portable Windows C-drive cleanup utility by **CraZyX**. This historical release preserves its original feature stage.

## Changes in this version

Add application discovery, external rule updates, offline import, and file backup restoration.

## Run

Extract `Clarity-beta0.05-portable.zip`, then run `ClearSpace.exe` with its matching `ClearSpace.exe.config` beside it. The interface is in Simplified Chinese. Windows 10/11 and .NET Framework 4.8 are required.

Use the Chinese README for this version's exact options, defaults, cleanup boundaries, and backup behavior. Review item explanations before deleting files; advanced selections can remove personal or diagnostic data. Later-version features are not part of this historical build.

## Source and verification

Build with `.\build.ps1 -OutputDirectory .\dist`. This archive includes the same historical source snapshot as its release tag. The renumbered executable passed 36 isolated checks; its Chinese verification report is included in the portable package. Tests did not clean real user caches.

[Download this release](https://github.com/CrazyHandin/Clarity/releases/tag/beta0.05) · [Full release history](CHANGELOG.md) · [Latest beta0.10](https://github.com/CrazyHandin/Clarity/releases/tag/beta0.10)

The repository's commit history is reconstructed from archived source snapshots. Publication timestamps are not original development dates.
