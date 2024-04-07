# LocalStorage

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that allows you to store items locally on your hard drive.<br>

> [!Note]
> Updated by Calamity Lime, pretty much the same as the original [Local Storage Mod](https://github.com/art0007i/LocalStorage) by art0007i but with some minor changes to suit me a bit better. This is a drop in compatible with the original Local Storage Mod and <i>should</i> work the other way as well just losing the thumbnails but no promises there. <br>
> <b>Also important to know:</b> I didn't test this that much, it should work fine but I could have missed something small.

<figure>
<figcaption><span class="label"><b>List of Changes:</b></span> </figcaption>
<ul>
<li> You can save items with the same name into the same folder.</li>
<li>Item thumbnails are saved to a dedicated folder since I like to clear the cache that Resonite dumps in the <b>AppData\LocalLow</b> folder.</li>
<li>Folder names have invalid characters removed from them automatically, this is reflected in Resonite. Resonite allowed file/folder names with characters that Windows does not allow in paths.</li>
<li>The <b>Local Storage</b> button is now brick red, I just like it but maybe I'll change it just to keep you on your toes, who knows.
</ul>
</figure>

> [!IMPORTANT]
> You might be wondering why I don't push my changes back to art0007i's GitHub repo, I'm just too lazy to figure out how to do that. I know how to fork and do my own thing, so I'll do that thanks.

> [!WARNING]
> This mod does not manage assets. If you have an item it will be saved as-is, so cloud assets will remain cloud assets and local assets will remain local assets.

<br>
This mod adds a new section to the inventory that can be accessed by pressing the cloud button, called Local Storage.
Any items saved to this section will be stored in a configurable path on your hard drive.<br>
It is possible to manually add/edit items and directories but make sure you know exactly what you are doing because things might break.

Also adds json file importing because apparently it was removed from Resonite.

## Installation
1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
1. Place [LocalStorage.dll](https://github.com/LimeProgramming/LocalStorage/releases/latest/download/LocalStorage.dll) into your `rml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\ResoniteVR\rml_mods` for a default install. You can create it if it's missing, or if you launch the game once with ResoniteModLoader installed it will create the folder for you.
1. Start the game. If you want to verify that the mod is working you can check your Resonite logs.
