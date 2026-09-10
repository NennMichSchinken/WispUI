# WispUI

A curated UI suite for FINAL FANTASY XIV, built as a [Dalamud](https://dalamud.dev/) plugin.

> **Status: pre-alpha.** Planning and design are done; the plugin itself is not built yet. Not usable, not released.

## What it is

FFXIV's party list is a set of narrow health bars with almost no control over size or layout. WispUI replaces it with **real rectangular unit frames** — free width, height, padding and spacing — the element the game does not offer.

The guiding idea is **curation, not parity**: a short list of elements that a serious player actually needs, each done properly, with good defaults instead of hundreds of switches.

## Design principles

- **Familiar where it counts.** The settings window follows the game's own system configuration — same structure, same font (the game's `Axis`), same accent gold. Nothing to learn.
- **Better where it matters.** The HUD deliberately does not copy the vanilla party list, because those narrow bars are the problem being solved.
- **Readable under pressure.** Defaults are designed from a healer's point of view: what reads at a glance for a healer reads for everyone.
- **Consistent on every screen.** One interface scale derived from the game's own screen size and UI base scale, with every size rounded to whole pixels.

## Planned for the first version

- Party frames for up to 8 members: health bar with current / percent / deficit text, MP bar, job icon, role or job colouring, player name
- Removable (Esuna-able) effects highlighted, using the game's own status data and party-list priority
- Free positioning by drag or by coordinates, role-based sorting with a custom role order
- **Arrow buttons instead of dropdowns** for texture and style selection, with a live preview and an optional searchable list
- **Copy and paste appearance** between elements, with per-area tick boxes — size and position are structurally excluded

Later: player health/mana/cast bars, job gauges, 24-man alliance layout.

## Requirements

- FINAL FANTASY XIV with XIVLauncher / Dalamud
- Built against Dalamud API 15, targeting `net10.0-windows`

## Licence

[MIT](LICENSE). WispUI is an independent project and is not affiliated with or endorsed by Square Enix.

FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd.
