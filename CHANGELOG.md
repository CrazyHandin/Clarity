# Clarity release history

[简体中文](版本历史.md) | **English**

Software versions follow `beta0.xx`, increasing with each release. The Windows numeric file version is `0.<release number>.0.0`. `version.json` is the build's single version source. Winapp2 dates and JSON rule versions are independent.

| Version | Original label | Changes |
| --- | --- | --- |
| beta0.10 | beta0.10 | Rename the default file backup folder to ClarityBackups; add the smaller Clarity name to the interface; make quick scan the default; curate built-in rules for common applications and add WeChat, QQ, Adobe, VS Code, and WPS cache and log rules. |
| beta0.09 | beta0.09 | Add regular and quick scanning; keep JSON and Winapp2 update addresses separate; open application file locations; group unknown caches under application submenus; confirm risky selections; add a cleanup completion screen; use Clarity.exe and a shared application icon; fix white result rows during cleanup. |
| beta0.08 | beta0.08 | Open backup locations; remove verified backup copies after successful restoration; move optional Windows Disk Cleanup to an independent cleanup checkbox. |
| beta0.07 | 3.2.0-beta.1 | Read unknown cache contents; provide explicit risk deletion choices with backups and confirmation; add optional Windows Disk Cleanup. |
| beta0.06 | 3.1 | Add Winapp2 INI parsing, import, updates, compatibility reporting, and restricted file-rule adaptation. |
| beta0.05 | 3.0 | Add application discovery, external rules, updates, offline import, and file backup restoration. |
| beta0.04 | 2.0.1 | Return to the preparation screen on rescan, requiring another manual scan start. |
| beta0.03 | 2.0 | Expand cleanup rules and category navigation; add restricted registry cleanup and clearer progress and risk labels. |
| beta0.02 | 1.1 | Add manual scan start, more cleanup categories, and interface refinements. |
| beta0.01 | 1.0 | Initial portable Windows C-drive cleaner with simple and advanced cleanup and item explanations. |

Historical releases preserve their original feature stages and were renumbered consistently. Versions through beta0.08 use `ClearSpace.exe`; beta0.09 and later use `Clarity.exe`. New users should start with beta0.10. All listed versions are beta releases.

The Git history is reconstructed from these archived source snapshots. GitHub import timestamps are publication timestamps, not the original development dates.
