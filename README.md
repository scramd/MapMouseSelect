# Map Mouse Select

A client mod for Nuclear Option. Drag a box on the map to pick your targets.

![Dragging a box over a group of units on the map](docs/media/marquee.gif)

In the stock game you click units on the map one by one. With this mod you hold the left mouse button, drag a box over them, and every unit inside becomes a target for your current weapon.

This is a fork of [Map Mouse Select (& more)](https://github.com/Metadorius/MapMouseSelect) by Kerbiter. It keeps the selection features and leaves out the rest. See [Where this comes from](#where-this-comes-from).

## Controls

All of these work on the expanded map.

| Control | What it does |
| --- | --- |
| Left drag | The units in the box become your targets. Your earlier targets are dropped. |
| Shift + left drag | Adds the units in the box to your targets. |
| Ctrl + left drag | Removes the units in the box from your targets. |
| Left click on a unit | Makes it your only target. |
| Shift + left click on a unit | Adds it. On a unit that is already a target, removes it. |
| Right drag | Moves the map. The place you grabbed stays under the cursor. |
| Right click | As in the stock game: sends the selected friendly vehicles and ships to that place. |

A box picks the same units a click could pick:

- It follows the filter buttons of the Target List. Units that a filter hides are not picked.
- It skips your own aircraft and airbases.
- A weapon takes at most 128 targets.
- A plain box with no units in it clears your targets.

The mod adds one filter button to the Target List: **NTL**. Switch it off to keep neutral units out of your selection. The stock buttons can only filter friendly and enemy units.

Without an aircraft the box selects units on the map in the same way a click does. With friendly vehicles or ships selected, a right click on the map sends them there, as in the stock game.

## What changes compared with the stock game

The box needs the left mouse button, so a few stock controls move:

- **Moving the map** goes from a left drag to a right drag. A right click without a drag still gives its order.
- **Keys bound to Shift or Ctrl alone**, such as a throttle key, do nothing while the map is open. Shift and Ctrl choose the kind of box there. Key combinations such as Ctrl+Shift+E still work.
- **A click replaces your targets.** In the stock game a click in flight adds a target. Hold Shift to add.

The mod also repairs three things in the stock game that selection runs into:

- A unit that is already selected can be clicked again, so Shift + click can remove it.
- The Target List no longer keeps rows for units that are no longer selected.
- A map drag follows the mouse exactly. In the stock game the map moves further the lower your frame rate is.

## Install

There is no release yet. This fork is in development. To try it, [build it](#build) and put `MapMouseSelect.dll` into `Nuclear Option/BepInEx/plugins/MapMouseSelect/`. You need BepInEx 5 (x64).

Do not install it together with the original mod. Both use the same plugin ID.

## Settings

The mod writes `BepInEx/config/com.kerbiter.mapmouseselect.cfg`. With the BepInEx ConfigurationManager installed you can change the settings in the game with F1. You can change the box colours and the map-drag button, and switch several of the repairs off. [All settings](docs/configuration.md).

## Limits

- The box needs a mouse. There is no controller control for it yet.
- While you hold the left button on the map, map zoom and map movement from a keyboard or controller pause until you release.
- The right button is also the game's Free Look control. While you hold it on the open map, the mod ignores Free Look, so a map drag does not turn the camera behind the map.
- In the mission editor the box is off by default. The editor has its own box selection.

## Where this comes from

This mod is a fork of **Map Mouse Select (& more)** by **Kerbiter** ([Metadorius/MapMouseSelect](https://github.com/Metadorius/MapMouseSelect)), from upstream commit [`6e08c30`](https://github.com/Metadorius/MapMouseSelect/commit/6e08c30fd6d5315d6ce84650c2f91956e2cb7ab7). The box selection, its drawing, the click behaviour, the map-drag changes, the neutral filter and the stock repairs are Kerbiter's work. The Git history keeps every upstream commit.

The fork started on 2026-11-10 with these changes:

- It builds for game version 0.34.6. The original no longer builds for the current game.
- Features that are not about selecting on the map are gone: the frame rate limit for an unfocused game, the key to leave a parked aircraft, icons for destroyed buildings, the three extra target keys, the "stick" way to scroll the map, the move cursor, and the camera wheel fix.
- The map is dragged with the right button, and a right click keeps its stock order. The original used the middle button and moved the camera jump to a double left click.

[What was kept, what was removed, and why](docs/scope.md).

The original code was written with heavy AI assistance, and so are the changes in this fork.

## Build

You need a .NET SDK and your own copy of the game with BepInEx 5.

```powershell
dotnet build src/MapMouseSelect/MapMouseSelect.csproj -c Release -p:NuclearOptionRoot="D:/Games/Nuclear Option" -p:DeployToPlugins=false
```

The result is `src/MapMouseSelect/bin/Release/MapMouseSelect.dll`. Without `-p:DeployToPlugins=false` the build also copies the DLL into the game's plugin folder.

For developers: [how it works](docs/architecture.md), [how it is tied to the game code](docs/coupling.md), [checks after a game update](docs/updating.md), [input notes for the next step](docs/input-next.md).

## Licence

GPL-3.0. See [LICENSE](LICENSE). Copyright (C) 2026 Metadorius. Changes in this fork are under the same licence.

This project is an unofficial community modification and is not affiliated with, sponsored by, or endorsed by Shockfront Studios Pty Ltd. Original Nuclear Option assets, vehicle designs, audio, and code are Copyright (c) 2026 Shockfront Studios Pty Ltd. All rights reserved. Nuclear Option and Shockfront Studios are trademarks or registered trademarks of Shockfront Studios Pty Ltd. Original mod content and all other trademarks belong to their respective owners.
