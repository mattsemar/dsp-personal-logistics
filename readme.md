Personal Logistics Free Mode

### Overview
This mod is an inventory management system that lets you have what you want when you want it.

It's not limited to fetching items from your logistics stations like other mods (plain Personal Logistics). But do check
out that mod's readme for more details about how to use this one.

### Plugin Cheat Level

The `Plugin Cheat Level` config option can be used to set how cheaty you want to get

* `Full` - (default) Doesn't bother with logistics stations for loading items, just gives you what you want in the amounts you want
* `Quarter` - (arbitrary fraction) Take items from any supplying stations but with no warpers or shipping energy cost
* `Half` - (also arbitrary) Take items from any station with no shipping cost (will use storage, supply or demand) 
* `Planetary` - Only take items from stations on local planet without shipping cost or delay

## Nebula 
This mod has been updated to be compatible with Nebula Multiplayer Mod. Note that it relies on the host to store the client's
state so the first time a client connects they won't have any requested items set up.
Note: Nebula Multiplayer mod itself is NOT required. Only its API plugin is a dependency

## How to install

This mod requires BepInEx to function, download and install it
first: [link](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation/index.html?tabs=tabid-win)

#### Manually

First install the [CommonAPI](https://dsp.thunderstore.io/package/CommonAPI/CommonAPI/) mod. Next install
the [DSPModSave](https://dsp.thunderstore.io/package/CommonAPI/DSPModSave/) mod. 
Next, install the [Nebula Multiplayer Mod API](https://dsp.thunderstore.io/package/nebula/NebulaMultiplayerModApi/) mod
Then, extract the archive file and drag `PersonalLogistics.dll` and `pui` into the `BepInEx/plugins` directory.

#### Mod manager

Click the `Install with Mod Manager` link above. Make sure dependencies are installed, when prompted

## Changelog

#### v1.0.0
First version, might delete later

## Contact

Bugs? Contact me on discord: Semar#1983 or create an issue in the github repository.

Icon credit, B.E. Cimino