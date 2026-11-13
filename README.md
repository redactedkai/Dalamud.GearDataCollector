# Dalamud.GearDataCollector

A read-only [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FFXIV. It records how your stats change as you remove or equip gear in a duty, and works out what each piece gives you under that duty's sync.

## Using it

1. Open the plugin with `/geardata` and tick **Enable data collection** on the **Config** tab.
2. Enter a duty and take off food and buffs.
3. On the **Duty / Gear Info** tab, press **Start session**.
4. Remove or equip one piece at a time, about 2 seconds apart.
5. Press **Stop session**.

Files are saved in the `snapshots` folder next to the plugin's DLL, or in the folder you pick on the Config tab.

## What it saves

- **Session file** (`.jsonl`): the duty details, your stats and gear at the start, then one line for each change. The first line's `baseStats` are your unsynced base stats, even in a synced duty; the total stats show what you have in the duty. A stat that drops to 0 is written as 0.
- **Pieces file** (`.pieces.json`): the stats each piece gave you in the duty. It is made when you stop, from changes of one piece at a time. The weapon and Soul Crystal can only be swapped, so they get no value.

The plugin also keeps a small temporary file, `duty-flag.json`, so it remembers the duty's Minimum Item Level setting if the game restarts. It is safe to delete.

## Building

### Requirements

- [.NET 10 SDK](https://dot.net/download). The project targets `net10.0-windows` to match Dalamud 15.x.
- [XIVLauncher](https://goatcorp.github.io/) with Dalamud installed. The build references assemblies from `%APPDATA%\XIVLauncher\addon\Hooks\dev\`. If Dalamud is installed elsewhere (for example on another drive), set the `DALAMUD_HOME` environment variable to that `Hooks\dev` folder before building.

### Build

```bash
git clone https://github.com/redactedkai/Dalamud.GearDataCollector.git
cd Dalamud.GearDataCollector
dotnet build Dalamud.GearDataCollector.sln -c Release
```

Output: `src\Dalamud.GearDataCollector\bin\x64\Release\Dalamud.GearDataCollector.dll`

### Loading as a dev plugin

1. Launch FFXIV through XIVLauncher.
2. In game, type `/xldev`, open **Dev Plugin Locations** and **Add** this folder:
   ```
   <path-to-repo>\src\Dalamud.GearDataCollector\bin\x64\Release
   ```
3. Click **Load**. After a rebuild, click **Reload** to load the new build without restarting the game.

### Running the tests

The tests run without the game:

```
cd tests/Dalamud.GearDataCollector.Tests
dotnet run
```
