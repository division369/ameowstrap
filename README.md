<!-- markdownlint-disable MD033 MD041 -->

> [!CAUTION]
> The only official place to download ExploitStrap is **this GitHub repository** — the [Releases page](https://github.com/RealSlimShady2000/MrExLiveChannelForcer/releases). Anywhere else offering "ExploitStrap" is not us. Don't download from them.

<p align="center">
  <img src="MrExStrap/Resources/ExploitStrap.png" alt="ExploitStrap" width="520">
</p>

<p align="center">
  <a href="./LICENSE"><img src="https://img.shields.io/badge/license-MIT-success" alt="License: MIT"></a>
  <a href="https://github.com/RealSlimShady2000/MrExLiveChannelForcer/actions/workflows/ci-release.yml"><img src="https://img.shields.io/github/actions/workflow/status/RealSlimShady2000/MrExLiveChannelForcer/ci-release.yml?branch=main&label=build" alt="Build status"></a>
  <a href="https://github.com/RealSlimShady2000/MrExLiveChannelForcer/releases/latest"><img src="https://img.shields.io/github/downloads/RealSlimShady2000/MrExLiveChannelForcer/latest/total?label=downloads%40latest&color=8b5cf6" alt="Downloads (latest release)"></a>
  <a href="https://github.com/RealSlimShady2000/MrExLiveChannelForcer/releases"><img src="https://img.shields.io/github/downloads/RealSlimShady2000/MrExLiveChannelForcer/total?label=downloads%40total&color=8b5cf6" alt="Downloads (all releases, all time)"></a>
  <a href="https://github.com/RealSlimShady2000/MrExLiveChannelForcer/releases/latest"><img src="https://img.shields.io/github/v/release/RealSlimShady2000/MrExLiveChannelForcer?label=release&color=8b5cf6" alt="Latest release"></a>
  <a href="https://discord.robloxscripts.com"><img src="https://img.shields.io/discord/1424371108244619377?label=discord&logo=discord&logoColor=white&color=5865F2" alt="Discord — join us"></a>
  <a href="https://github.com/RealSlimShady2000/MrExLiveChannelForcer/stargazers"><img src="https://img.shields.io/github/stars/RealSlimShady2000/MrExLiveChannelForcer?label=stars&color=f59e0b" alt="GitHub stars"></a>
</p>

<p align="center">
  <a href="https://github.com/RealSlimShady2000/MrExLiveChannelForcer/releases/latest/download/ExploitStrap.exe">
    <img src="https://img.shields.io/badge/%E2%AC%87%20Download%20ExploitStrap-Latest%20release-22D3EE?style=for-the-badge&logo=windows&logoColor=white" alt="Download the latest ExploitStrap release">
  </a>
</p>

**The Roblox launcher built for executor and exploit users.** A fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) hardened against the things that break executors — surprise channel routing, updates that ship before your tool catches up, and ban traces left on your machine — plus a load of quality-of-life extras.

> [!NOTE]
> Windows 10 and above. Built for Roblox exploit / executor users — if you only play vanilla Roblox, you probably don't need most of what's here.

## Features

- **LIVE channel lock** — forces Roblox onto production every launch, fixing most "my executor broke after a Roblox update" cases.
- **Versions Manager** — one saved profile per executor with its own isolated install folder, one-click switching, and auto-updates from weao.xyz.
- **One-click downgrading** — pin any historical Roblox build (with CDN verification), or use "Match your executor" to auto-pick the right one.
- **BanAsync tools** — clean Roblox traces, spoof your network MAC, randomize MachineGuid, and wipe only your Roblox cookies (other sites untouched).
- **Multi-instance** — run several Roblox clients at once, auto-arranged into a tidy grid.
- **VIP server picker** — join a free shared VIP server before launch.
- **Fast Flag editor** — edit flags the safe way (config file, not process injection), with a banner spelling out what actually gets you banned.
- **Auto-update** — real progress bar, fires both on launch and when you open the menu.
- **Privacy by default** — tracking cookies wiped before every launch, analytics hardcoded off.
- **Clear error messages** — failures tell you the real reason (DNS, TLS, rate limit, disk full…), not "something went wrong".

## Install

1. Download the latest `ExploitStrap-vX.Y.exe` from the [Releases page](https://github.com/RealSlimShady2000/MrExLiveChannelForcer/releases).
2. Run it. It's self-contained (no .NET install needed) and lands in `%localappdata%\ExploitStrap`.

To uninstall: **Windows Settings → Apps**, search "ExploitStrap" — or run `ExploitStrap.exe -uninstall`.

## Unsigned build & antivirus

Releases ship **unsigned**, so Windows SmartScreen — and sometimes Defender, as `Wacatac.H!ml` — may warn on first run. **It's a false positive.** An unsigned single-file .NET app that legitimately touches the registry, spoofs your MAC, and cleans cookies is exactly what trips machine-learning heuristics. Code signing is on the way and will stop these for good.

Want to be sure your copy is genuine? Check its SHA-256 against the `SHA256SUMS` on the release, scan it on [VirusTotal](https://www.virustotal.com), or build it yourself.

<details>
<summary><b>Build it yourself · recover a quarantined file</b></summary>

```
git clone --recurse-submodules https://github.com/RealSlimShady2000/MrExLiveChannelForcer.git
cd MrExLiveChannelForcer
dotnet publish MrExStrap/ExploitStrap.csproj -p:PublishSingleFile=true -r win-x64 -c Release --self-contained true
```

Output lands at `MrExStrap/bin/Release/net6.0-windows/win-x64/publish/ExploitStrap.exe`.

If Defender already quarantined it: **Windows Security → Virus & threat protection → Protection history → Restore**, then add an exclusion for `%localappdata%\ExploitStrap`. If the auto-updater's download keeps getting flagged, grab the new release manually from GitHub. Each release is submitted to Microsoft as a false positive, which usually clears within a few days.

</details>

## Which launcher?

| Pick this if you… | Use |
| --- | --- |
| Run executors/externals and want them to keep working | **ExploitStrap** |
| Want a polished player launcher with broad theme support | Fishstrap |
| Want official vanilla Bloxstrap with the largest user base | Bloxstrap |

## Credits & support

Vibe pasted by **Sir Meme** — in the Roblox community since 2017, formerly Synapse Softworks LLC, now runs [robloxscripts.com](https://robloxscripts.com) and [rsware.store](https://rsware.store). Vibe coded with Claude.

Found a bug? [Open an issue](https://github.com/RealSlimShady2000/MrExLiveChannelForcer/issues) or ask in the [Discord](https://discord.robloxscripts.com).

## License

[MIT](./LICENSE), inherited from [vanilla Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by pizzaboxer et al. This fork's changes are © 2026 RealSlimShady2000.
