ParaPie 2.0.2 - Sims-style pie menus for Paralives
=================================================

Replaces the Together Card system with a classic life-sim pie menu.
Click another Para to pick any social whenever you like; no together bar.

INSTALL (Windows, Steam)
1. In Steam: right-click Paralives > Manage > Browse local files.
2. Copy EVERYTHING from this zip into that folder (the one with Paralives.exe).
   You should end up with winhttp.dll and a BepInEx folder next to Paralives.exe.
3. Start the game. The first launch takes a little longer while BepInEx sets up.

Already have BepInEx 5 installed? Only copy BepInEx/plugins/ParaPie/ParaPie.dll.

HOW TO PLAY
- Click another Para: a pie opens with every social you qualify for (grouped as
  Friendly, Funny, Romantic, Mean...) plus their normal interactions.
- Socials need the two Paras to be hanging out: use "Talk with" from the pie first.
- Click anywhere outside the pie to close it.

KEYS
  Ctrl+F7  turn the mod on/off (back to the original cards and bar)
  Ctrl+F8  show a sample pie
  Ctrl+F9  unstick: if the game ever seems stuck paused after a social

SETTINGS
  BepInEx/config/com.parapie.paralives.piemenu.cfg  (created after the first launch)

UNINSTALL
  Remove only the mod:  delete BepInEx/plugins/ParaPie
  Remove everything:    delete winhttp.dll, doorstop_config.ini, .doorstop_version and the BepInEx folder

PROBLEMS
  Send BepInEx/ParaPie.log. If a Paralives update breaks the mod, it switches
  itself off and the game uses its original UI.

Not affiliated with Paralives Studio. Made for Paralives Early Access 0.1.9b.

Includes BepInEx 5.4.23.2 (https://github.com/BepInEx/BepInEx, LGPL-2.1) and
HarmonyX, unmodified, as the mod loader.
