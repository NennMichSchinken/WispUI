# WispUI

A curated UI suite for FINAL FANTASY XIV, built as a [Dalamud](https://dalamud.dev/) plugin.

## What it is

FFXIV's party list is a set of narrow health bars with almost no control over size or layout. WispUI replaces it with **real rectangular unit frames** — free width, height, padding and spacing — the element the game does not offer.

The guiding idea is **curation, not parity**: a short list of elements that a serious player actually needs, each done properly, with good defaults instead of hundreds of switches.

## Features

**Party frames**
- Frames you size yourself, in the party order you already set in the game
- Health bar in nine looks, shields in five, coloured by role or job
- Effect icons in three rows — debuffs, your own effects, everyone else's — with time left and a sweep
- A mark on anyone who needs cleansing and on anyone being raised
- A ring around the frame of whoever you have targeted
- Click to target, right-click for the game's own menu, and mouse bindings per job
- Mouseover casting, chosen one spell at a time
- A live preview in the settings window, so you can set everything up without a party

**Damage meter**
- A combat tracker in the same look, fed by [IINACT](https://www.iinact.com/)
- Without IINACT the module stays inactive and walks you through installing it; nothing else in the suite is affected

**Everywhere**
- Different settings per job or role, switched when you change job
- Share your whole setup as one line of text
- Your own colour for every job and role
- Six fonts, three weights, and a folder for your own
- **Arrow buttons instead of dropdowns**, with a live preview and an optional searchable list
- **Copy and paste appearance** between elements

## Installation

WispUI is distributed through its own plugin repository, because damage meters are not eligible for the official one.

1. In game, open the Dalamud settings with `/xlsettings`.
2. Go to the **Experimental** tab.
3. Under **Custom Plugin Repositories** (the lower of the two lists), add this URL:

   ```
   https://raw.githubusercontent.com/NennMichSchinken/WispUI/main/repo.json
   ```

4. Click the **+** to add it, then **Save**.
5. Open the plugin installer with `/xlplugins`, search for **WispUI**, and install it.

The party frames are off until you turn them on, so nothing changes on screen until you ask for it.

## Usage

- `/wisp` — open the settings

## Requirements

- FINAL FANTASY XIV with XIVLauncher / Dalamud (API 15)
- [IINACT](https://www.iinact.com/) for the damage meter only

## Licence

[MIT](LICENSE). WispUI is an independent project and is not affiliated with or endorsed by Square Enix.

Bundled third-party material:
- Icons from [Lucide](https://lucide.dev/) — ISC, see [Icons/LUCIDE-LICENSE.txt](Icons/LUCIDE-LICENSE.txt)
- Figtree and DM Sans — SIL Open Font License, see [Fonts/](Fonts/)

FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd.
