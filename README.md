# Star Wars: Echoes of War
Server-side C# code used in the Neverwinter Nights Star Wars: Echoes of War module, forked from Zunath's SWLOR https://github.com/zunath/SWLOR_NWN . This repository holds its C# server, module content, and desktop toolset.

Game: Neverwinter Nights: Enhanced Edition

Discord: https://discord.gg/qBgR9hJefs

# Project Description

This project contains the C# source code used on the Star Wars: Legends of the Old Republic server. 

It serves as a replacement for NWScript and handles most server features and functions. This is possible by using the NWNX_DotNet plugin for NWNX.

Refer to the quick start guide below and be sure to post any issues on our forums. The link to the forums is above.

# Prerequisites: 

Git: https://git-scm.com/downloads
  
Docker: https://www.docker.com/products/docker-desktop/
  
Visual Studio 2022: https://www.visualstudio.com/downloads/

.NET 10 SDK: [Download .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

Neverwinter Nights: https://store.steampowered.com/app/704450/Neverwinter_Nights_Enhanced_Edition/

# Installation:

1. Fork this repo.

2. At command line: ``git clone --recursive https://github.com/<your_username>/SWLOR_NWN.git``

3. Run the Setup script from the top level folder.
  
Setup initializes the pinned `SWLOR_Haks` submodule, prepares the native NWN tools, and builds the toolset. On Linux it downloads checksum-verified tools into `~/.local/share/SWLOR.Toolset/NativeTools`. It needs no Python and no IDE.

| Windows | Linux | What it does |
|---|---|---|
| `Setup.cmd` | `./Setup.sh` | Prepares the checkout and builds the toolset |
| `CheckSetup.cmd` | `./CheckSetup.sh` | Lists missing prerequisites without changing submodules, native tools, or containers (builds the small checker on first run) |
| `StartToolset.cmd` | `./StartToolset.sh` | Builds and opens the toolset |
| `StartServer.cmd` | `./StartServer.sh` | Packs the module, deploys it, and runs a local server (slow the first time; Ctrl+C stops it) |

To run a local server, also install Docker Desktop (Linux containers) on Windows, or Docker Engine with the Compose plugin on Linux. Check it with `CheckSetup.cmd -Profile Server` or `./CheckSetup.sh --profile Server`. To join the server, you need NWN:EE.

# Troubleshooting

**"Install a stable .NET 10 SDK" or "compatible SDK not found":** `global.json` accepts only stable .NET 10 SDKs. Install one, reopen your terminal, and run `dotnet --version` in the checkout. It must print `10.x.x` without a `-rc` or `-preview` suffix.

**'TLK does not exist' error:**

This happens because you didn't clone with the --recursive command. Redo step 2.

**'Type initializer for Ductus.FluentDocker.Extensions.CommandExtensions threw an exception' error:**

You need to install docker.

**Missing assets or TLK:** run `Setup.cmd` or `./Setup.sh`.

**"The native tool cache is incomplete":** close the toolset, delete the folder named in the message, and rerun setup.

**Docker unavailable:** start Docker, then rerun the server check above.

# Getting Help

If you need help with anything related to Star Wars: Echoes of War please feel free to contact us on our Discord here: https://discord.gg/qBgR9hJefs

For NWNX and Docker related issues please look for help in the NWNX Discord channel here: https://discord.gg/m2hJPDE
