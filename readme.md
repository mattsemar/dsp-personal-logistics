﻿Personal Logistics

Inventory management system. Set banned items that will be added to logistics stations. 
Set required items (with counts) and items will be fetched from logistics stations on your behalf

Open the request management window using this button
![Config](https://github.com/mattsemar/dsp-personal-logistics/blob/main/Examples/ex2.png?raw=true)

Set items that are not allowed to be in inventory
![Ban](https://github.com/mattsemar/dsp-personal-logistics/blob/main/Examples/ex3.png?raw=true)

Set items that should be kept in inventory
![Requested](https://github.com/mattsemar/dsp-personal-logistics/blob/main/Examples/ex4.png?raw=true)

Trash can be sent to logistics network also (disable using SendLitterToLogisticsNetwork config property)
![Requested](https://github.com/mattsemar/dsp-personal-logistics/blob/main/Examples/TrashManagement.gif?raw=true)

## Details

### Usage
This mod is intended for players who have completed the main objectives of the game and want to optimize their
endgame experience. It does not alter save games in any way, and as of the latest release available on 2021-Oct-04
does not trigger the game's abnormality checks so should not affect achievements or milestones. Its intent is to
improve QOL and a good deal of effort has been taken to respect the game's built-in costs for item transportation. 
Logistics vessel speed is the limiting factor on item deliver (as well as warper availability) so leveling up vessel speed
should provide noticeable increases in transportation time

### Buffer
Requested items are loaded into a local buffer which requests 1 logistic vessel capacity worth at a time. So, even if you request 15 conveyor belts 1000 will be loaded into your 
local buffer. This is done to save on warpers & energy needed by vessels for transporting items. It also allows for faster loading when laying down blueprints, for example.

The buffer is persisted locally on your filesystem (Documents\Dyson Sphere Program\PersonalLogistics) so that items that are expensive 
to build are not lost.

To clear your local buffer of an item type, you can set that item to be neither requested nor banned. The buffer won't be cleared immediately, so if you are uninstalling the mod,
make sure to look at the Buffered items window (click `Buffered` button in the config window) to make sure everything is returned
to either your inventory or to logistics stations.

### Litter
By default, littered items will be sent to the closest logistics station with capacity to hold them. This can be disabled
using the SendLitterToLogisticsNetwork config property. Litter, like banned items are first sent to the local buffer
where they will be automatically sent to stations (provided the item type is not currently requested).

### Mecha
In some cases, warpers and energy from Icarus will be used. This mostly happens when the nearby source for an item is
a station with low energy or no warpers. When this happens a UI message will be shown.


## How to install

This mod requires BepInEx to function, download and install it
first: [link](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation/index.html?tabs=tabid-win)

#### Manually

Extract the archive file and drag `PersonalLogistics.dll` and `pls` into the `BepInEx/plugins` directory.

#### Mod manager

Click the `Install with Mod Manager` link above.

## Changelog


#### v1.0.2
Added buffered item view, moved button to avoid conflict with other mods. Made sending trash to network default to true

#### v1.0.1
First version

## Contact
Bugs? Contact me on discord: mattersnot#1983 or create an issue in the github repository.

Icon credit, B.E. Cimino