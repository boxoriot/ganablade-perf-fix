# GanaBlade Perf Fix

An unofficial [BepInEx](https://github.com/BepInEx/BepInEx) plugin that fixes two
CPU performance bugs in **GanaBlade** (Unity 2019.4, 64-bit Mono).

> Not affiliated with or endorsed by the game's developer. Use at your own risk.

## What it fixes

The game is CPU bound. Two hot paths were responsible:

1. **`ControlUtil.GetButtonPush` (game-wide).** Input polling called
   `Enum.GetValues(typeof(KeyCode))` and `value.ToString().ToUpper()` over ~320
   key codes **on every call**, and the method runs 17x per frame — thousands of
   `Input.GetKey` calls and string allocations every frame, just to read the
   controller. Replaced with an O(1) cached name `KeyCode` lookup.

2. **Per-frame `FindObjectsOfType` in the weapon code (weapons 2 & 3).**
   `PlayerWing.SkillDo` (per wing) and `PlayerFire.DiuMission` call
   `FindObjectsOfType<…>()` every frame — `Fire4SuperGun` (weapon 3 / laser),
   `Fire2Super` and `SuperB2` (weapon 2 on each ship), `FireB1_SuperPlane`, etc.
   `FindObjectsOfType` scans the whole scene and allocates an array each call —
   brutal while the screen is full of pooled objects. The laser type gets a
   zero-scan live cache (populated as `Fire4SuperGun` spawn); every other type
   gets a frame-coherent cache (the real scan runs at most once per type per
   frame). All call sites are existence/count checks, so this is
   behavior-preserving.

Both patches reproduce the game's original behavior exactly; they only change
*how* the work is done.

## Requirements

- GanaBlade (64-bit build)
- [BepInEx 5.4.x **x64**](https://github.com/BepInEx/BepInEx/releases) (the Mono
  build — *not* BepInEx 6 / IL2CPP)

## Download

Grab the latest `GanaBladePerfFix.dll` from the
[**Releases**](../../releases) page. (It's a prebuilt binary — you do **not** need
to build from source to use the mod. Building is only needed if you want to
modify it; see [Building from source](#building-from-source).)

## Installation

1. **Install BepInEx 5.4.x x64.** Extract the BepInEx zip into the game folder —
   the one containing `GanaBlade.exe`. You should end up with `winhttp.dll`,
   `doorstop_config.ini`, and a `BepInEx/` folder next to the exe.
2. **Run the game once, then close it.** This makes BepInEx generate its
   `BepInEx/plugins/` and `BepInEx/config/` folders.
3. **Drop in the plugin.** Copy the downloaded `GanaBladePerfFix.dll` into
   `<game>/BepInEx/plugins/`.
4. **Launch and verify** (see below).

### Linux / Steam Proton (extra step)

BepInEx injects through `winhttp.dll`, which Proton won't load unless you tell
Wine to. In Steam: **right-click the game → Properties → Launch Options**, set:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

(Native Windows needs no launch option — `winhttp.dll` loads automatically.)

### Verify it loaded

Open `<game>/BepInEx/LogOutput.log` and look for:

```
[Info :GanaBlade Perf Fix] GanaBlade Perf Fix 1.2.0 loaded: per-frame-cached
FindObjectsOfType in PlayerWing.SkillDo + PlayerFire.DiuMission (weapons 2 & 3)
and O(1) ControlUtil.GetButtonPush input polling
```

If `LogOutput.log` doesn't exist after launching, BepInEx didn't inject (on
Linux, re-check the launch option above).

## Uninstall

Delete `GanaBladePerfFix.dll` from `BepInEx/plugins/`. To remove BepInEx
entirely, delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`, the
`BepInEx/` folder, and the launch option.

## Building from source

You need the [.NET SDK](https://dotnet.microsoft.com/download), a local copy of
the game, and BepInEx already installed into it (the build references the game's
own assemblies and BepInEx's — none are included in this repo).

```sh
dotnet build -c Release -p:GameDir="/path/to/GanaBlade"
# output: bin/Release/GanaBladePerfFix.dll
```

Set `GameDir` to the game install folder — the one containing `GanaBlade.exe` and
`GanaBlade_Data`.

## License

Plugin source: MIT (see `LICENSE`).
