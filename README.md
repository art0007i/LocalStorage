# LocalStorage

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that allows you to store items locally on your hard drive.<br>

> [!WARNING]
> This mod does not manage assets. If you have an item it will be saved as-is, so cloud assets will remain cloud assets and local assets will remain local assets.
>
> Also saving trying to save items with invalid path characters in their name is not supported.

This mod adds a new section to the inventory that can be accessed by pressing the cloud button, called Local Storage.
Any items saved to this section will be stored in a configurable path on your hard drive.<br>
It is possible to manually add/edit items and directories but make sure you know exactly what you are doing because things might break.

Also adds json file importing because apparently it was removed from Resonite.

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
1. Place [LocalStorage.dll](https://github.com/art0007i/LocalStorage/releases/latest/download/LocalStorage.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\ResoniteVR\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create the folder for you.
1. Start the game. If you want to verify that the mod is working you can check your Resonite logs.
