Personal Logistics

Inventory management system. Set banned items that will be sent to logistics stations whenever they are in your inventory. Set min and max allowed amounts for items and items will
be fetched from logistics stations on your behalf. Also supports sending trashed items to logistics stations

Open the request management window using this button
![Config](https://github.com/mattsemar/dsp-personal-logistics/blob/main/Examples/ex2.png?raw=true)

Recycle items from inventory by dropping them into the Recycle window. To disable, set the `ShowRecycleWindow` config property to false. Items added here will first go the local
Buffer (details below) and then will be sent to the nearest Logistics Station with capacity. Of course, if you try and recycle an item you're currently requesting, it's just going
to come back to your inventory.

Note that items that have no Logistics Stations will immediately be returned to inventory (or trashed if no space).
![Recycle](https://github.com/mattsemar/dsp-personal-logistics/blob/main/Examples/Recycle.png?raw=true)

Configure mod to always keep 5 stacks of Plane smelters in inventory and also to send extra Plane smelters to logistics stations (when you have more than 5 stacks) 
![Ban](https://github.com/mattsemar/dsp-personal-logistics/blob/main/Examples/ex3.png?raw=true)

Keep that crude oil out of inventory completely.
![Requested](https://github.com/mattsemar/dsp-personal-logistics/blob/main/Examples/ex4.png?raw=true)

Trash can be sent to logistics network also (disable using SendLitterToLogisticsNetwork config property)
![Requested](https://github.com/mattsemar/dsp-personal-logistics/blob/main/Examples/TrashManagement.gif?raw=true)

## Details

### Usage

This mod does not alter save games in any way, and as of the latest release available on 2021-Dec-02 does not trigger the game's abnormality checks so should not affect
achievements or milestones. Its intent is to improve QOL and a good deal of effort has been taken to respect the game's built-in costs for item transportation. Logistics vessel
speed is the limiting factor on item delivery (as well as warper availability) so leveling up vessel speed should provide noticeable increases in transportation time

### Buffer

Requested items are loaded into a local buffer which requests 1 logistic vessel capacity worth at a time. This is done to save on warpers & energy needed by vessels for transporting items. It also allows for faster loading when laying down blueprints.

The buffer is persisted locally next to your game save (using DSPModSave) so that your items won't get lost if you load up a different save.

To clear your local buffer of an item type, you can set that item to be neither requested nor banned (Request 0, Recycle Inf). The buffer won't be cleared immediately, so if you are uninstalling the mod,
make sure to look at the Buffered items window (click `Buffered` button in the config window) to make sure everything is returned to either your inventory or to logistics stations.

### Litter

By default, littered items will be sent to the closest logistics station with capacity to hold them. This can be disabled using the SendLitterToLogisticsNetwork config property.
Litter, like banned items are first sent to the local buffer where they will be automatically sent to stations (provided the item type is not currently requested).

Littered items are not completely intercepted to try and avoid affecting the game's responsiveness. Instead, when littered items are detected a task is created that gets processed
later. That task will only cleanup litter that is less than 1km away from the player, (so basically on the local planet). Because of this, some litter may be missed.

### Mecha

In some cases, warpers and energy from Icarus will be used. This mostly happens when the nearby source for an item is a station with low energy or no warpers. When this happens a
UI message will be shown.

#### Add Fuel to Mecha Fuel Chamber

The plugin can be configured to automatically keep your Mecha's fuel chamber filled. This must be enabled in the Config section (Add fuel to mecha fuel chamber). Which items are
used are decided in the using these priorities (highest priority first)

* Item is currently being burned by Mecha and either an empty slot is available or a partially filled slot for that item type exists (top priority)
* Item is in one of the slots, but stack is not full. Note that empty slots will be filled with this item, if available
* Item is requested from logistics network and empty slot exists for it
* Item is fuel - any available fuel in inventory will be used to fill Mecha, starting with the fuel with the highest energy

#### Add Warpers To Mecha

Similar to adding fuel, the mecha's supply of warpers can be kept topped-off using this setting in the Config section of UI. This will only be done automatically if the logistics
network has warpers available and the requested minimum for warpers is at least 1 (can't be banned or unset).

### Build Preview Navigation (removed)

This functionality has been moved to a separate plugin, [LongArm](https://dsp.thunderstore.io/package/Semar/LongArm). See FlyToBuild under the Build Helper Modes section

### Request Modes

The `Station Request Mode` config option can be used to change which stations will be used for supplying your mecha's inventory. The different modes are

* All - (default) Takes items from any station that has it, regardless of supply or demand. Closest stations have highest priority
* AnySupply - Takes items from any station with item set to, "Supply" (PLS) or "Remote Supply", "Local Supply" (ILS)
* IlsDemandRules - Follows the same rules as a nearby ILS with Remote Demand/Local Demand set. Will not take from PLS on other planets
* IlsDemandWithPls - Same as IlsDemandRules but also takes from PLS on other planets set to Supply

Note that the last two have not been as extensively tested as the first 2, and reproducible bug reports are always welcome

## How to install

This mod requires BepInEx to function, download and install it
first: [link](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation/index.html?tabs=tabid-win)

#### Manually

First install the [CommonAPI](https://dsp.thunderstore.io/package/CommonAPI/CommonAPI/) mod. Next install
the [DSPModSave](https://dsp.thunderstore.io/package/CommonAPI/DSPModSave/) mod. Next, extract the archive file and drag `PersonalLogistics.dll` and `pui` into
the `BepInEx/plugins` directory.

#### Mod manager

Click the `Install with Mod Manager` link above. Make sure dependencies are installed, when prompted

## Changelog

#### v2.0.4
Feature: Added popup confirmation before the first time trashed items are recycled automatically.    

#### v2.0.3
Features:
* Added tip for +/- buttons to indicate shift/control for 5 or max 
* Added configurable minimum delay for recycle area. Gives more time to get items back if they were accidentally added   

#### v2.0.2
Bugfix: handled destruction of logistics station while items are being removed from it
Bugfix: fixed issue where recycled item icons were not appearing (blank white square)

#### v2.0.1
Bugfix: resolved issue where request window would not open until after inventory was first opened.

#### v2.0.0
* Overhauled UI for configuring requested items. Updated tooltips to refer to the number of stacks requested/auto-recycled instead of counts. Legacy request config window
is left in place for now, in case of bugs. It can be accessed by clicking the Settings button in the Request Window

Started some work on supporting localization. Sadly the only translations right now come either from the game (by re-using labels that the game uses) or from Google Translate.
This is very much a WIP so please send along any recommendations for better translations.

#### v1.6.3
* Bugfix: Fix issue with commonapi submodule not being properly initialized. Thanks to tier1thuinfinity on github for bugreport.

#### v1.6.2
* Bugfix: Disable recycle window as Shift/CTRL click target when a station window is open. Also handle the case where other storage window was opened after inv. window

#### v1.6.1
* Feature: Updated Recycle window to allow shift-clicking items to/from inventory. Also added non-persistent checkbox for hiding Recycle section temporarily

#### v1.6.0

* Feature: Added Recycle window where items from inventory can be dropped and automatically sent to Buffer (and then to stations, assuming the item isn't currently requested)

#### v1.5.0

* Switched to storing buffered items and inbound requests using DSPGameSave mod

#### v1.4.0

* Added options to keep Mecha fuel and warpers topped off from player inventory (see Mecha section for more info)
* Updated Incoming Items UI to include amount incoming and changed font to match game UI a little better

#### v1.3.1

Bugfix, fixed issue where planetary bot speed (vs interplanetary vessel speed) would be used for players who have not unlocked warp drive capability. (Thanks to Tivec for bug
report)

* Added 'Cancel requests' button to Actions section to allow canceling inbound requests that have been assigned an arrival time
* Added value to Config section to let player set max time in seconds to wait for item arrival (default 10 minutes)

#### v1.3.0

* Update UI button layout
* Added Clear Buffer button to quickly return all buffered items to Logistics Stations
* Removed fly to build functionality (moved to another plugin, LongArm)

#### v1.2.1

Bugfix, fixed issue in logic of IlsDemandRules mode where remote supply available counts were not being set

#### v1.2.0

Add ability to order mecha to fly to nearest Build Preview location. CTRL+R to toggle (see Build Preview Navigation section above)
Added config option for controlling what types of stations are pulled from. (see Request Modes section for more info)

#### v1.1.2

Bugfix, fixed issue issue where old reference was kept after loading new game save

#### v1.1.1

Bugfix, fixed concurrent modification issue with logistics network Fixed issue where management window close button was not shown

#### v1.1.0

* Fixed longitude labeling for build ghost geo coords (both east and west were labeled 'E')
* Updated inventory checker to count players hand items (so more won't be requested if you pick up all foundation, for example)
* Adjusted incoming items position when using items from inventory for research
* Adjusted logistics drone speed calculation to match game a little better
* Fixed missed sorting of player inventory
* Removed ability to send buffered items to inventory (or network) if there are incoming requests being processed for them (credit: ghostorchidgaming for report)

#### v1.0.9

Bugfix, fixed issue where incoming items would never be shown, oops.

#### v1.0.8

Added indicator text for nearest build ghost (disable with config option ShowNearestBuildGhostIndicator). Added config option to hide incoming items text (ShowIncomingItemProgress)
Adjusted incoming items indicator text positioning

#### v1.0.7

Updated handling of buffered item removal to time it better with inventory insertion. Began preliminary work on support for bypassing buffering for certain items

#### v1.0.5

Fixed bug with handling of max requested amounts. Updated usage of mecha core energy for logistics tasks to align better with the game's implementation

#### v1.0.4

Switched buffered item age to be based off of game time so that buffered items are not expired immediately after loading a savegame

#### v1.0.3

Updated action button position

#### v1.0.2

Added buffered item view, moved button to avoid conflict with other mods. Made sending trash to network default to true

#### v1.0.1

First version

## Contact

Bugs? Contact me on discord: mattersnot#1983 or create an issue in the github repository.

Icon credit, B.E. Cimino