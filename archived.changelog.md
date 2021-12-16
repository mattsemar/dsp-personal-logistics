## Changelog

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
