# LocalStorage
[![Thunderstore Badge](https://modding.resonite.net/assets/available-on-thunderstore.svg)](https://thunderstore.io/c/resonite/)

A [Resonite](https://resonite.com/) mod that allows you to store items locally on your hard drive.

> [!WARNING]
> This mod does not manage assets. If you have an item it will be saved as-is, so cloud assets will remain cloud assets and local assets will remain local assets. This means that if you wipe your local database, all local assets will be deleted with it, which might cause some of your locally saved items to lose assets.
>
> Also trying to save items with invalid path characters in their name is not supported.
>
> Also saving an item with the same name as another causes the old one to get overwritten.

This mod adds a new section to the inventory that can be accessed by pressing the cloud button, called Local Storage.
Any items saved to this section will be stored in a configurable path on your hard drive.<br>
It is possible to manually add/edit items and directories but make sure you know exactly what you are doing because things might break.

Also adds json file importing because apparently it was removed from Resonite.

## Installation (Manual)
1. Install [BepisLoader](https://github.com/ResoniteModding/BepisLoader) for Resonite.
2. Download the latest release ZIP file (e.g., `art0007i-LocalStorage-1.0.0.zip`) from the [Releases](https://github.com/art0007i/LocalStorage/releases) page.
3. Extract the ZIP and copy the `plugins` folder to your BepInEx folder in your Resonite installation directory:
   - **Default location:** `C:\Program Files (x86)\Steam\steamapps\common\Resonite\BepInEx\`
4. Start the game. If you want to verify that the mod is working you can check your BepInEx logs.
