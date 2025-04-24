SPTarkov "Pause" mod.

Forked and updated for 3.10.5+.
Tested on both 3.10.5 and 3.11.1.

## What gets paused
- You
  - Character control
  - Health
  - Hydration & Energy
  - Stamina
- AI
- The actual game raid timer
- The fake raid timer you see when you press o
- Time of Day

## Stuff that doesn't really pause well now
Stuff that doesn't pause well at the moment and may not be worth the effort.
- Ragdolls, physics stuff in general
  - Grenades will fly but the fuses won't tick until you unpause
- You jumping (instant ice skating)
- Certain oxygen/stamina effects
  - Pause while aiming will continue to drain oxygen
- You can pause and still move inventory around (shouldn't be able to use anything)
  
## Todo & Notes
- Look into pausing time of day
  - GameTimeClass.TimeFactor
  - GameTimeClass.TimeFactorMod
- patch GClass714.Update to stop oxygen while ads (prob stam regen too)
