# Clarity

[Chinese](README.md) | **English**

Clarity is a Windows C-drive cleaner with installer and portable editions. It cleans system junk and common application caches, with backup and restore support.

[Download latest](https://github.com/CrazyHandin/Clarity/releases/latest) · [User guide (Chinese / English)](docs/USAGE.txt) · [Changelog](CHANGELOG.md)

## Features

- **Simple and advanced cleanup:** Clean eligible low-risk items or select individual items with descriptions and risk information.
- **Two scan modes:** Quick uses built-in rules; Regular adds application discovery, external rules and unknown cache detection. Local caching reduces repeated identification work. Scanning starts only when requested.
- **Extensible rules:** Winapp2 is enabled by default and works alongside JSON, with multi-file JSON import, individual toggles, previews, upgrades and rollback.
- **Backup and restore:** Backup-first cleanup and saved cleanup history. Verified restored copies are automatically removed from backups.
- **Interface and updates:** Chinese and English, light and dark themes with Windows theme matching, experimental Liquid Glass styling, and software updates from About.

## Get started

Requires **Windows 10/11 and .NET Framework 4.8**.

1. Run the installer, or extract the complete portable package and run `Clarity.exe`.
2. Choose a scan mode and scope, then click **Start scan**.
3. Review results and risks, then use Simple or Advanced cleanup. Manage backups and rules under **Settings & Backups**.

Removing caches may require downloads or slow the next launch. Unknown caches and community rules are unchecked by default, advanced-only and backed up before cleanup.

The default backup folder is `ClarityBackups`. Backups on C: must be removed to free their corresponding space. Optional **Windows Disk Cleanup** bypasses Clarity backups.

## Documentation and build

[JSON rule example](rules/RulePack.template.json) · [Winapp2 guide (Chinese)](docs/Winapp2-使用说明.md) · [Build instructions](docs/BUILD.md)

Build with C# / WPF and the .NET Framework compiler:

```powershell
.\build.ps1 -OutputDirectory .\dist
```

## License

Clarity uses the [MIT license](LICENSE.txt). Winapp2 rules retain their separate **CC BY-SA 4.0** license; see [third-party notices](THIRD_PARTY_NOTICES.md).
