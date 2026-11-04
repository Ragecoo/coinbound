HUD 165 x 57. Point filtering.
Draw health_fill at (49,20) and mana_fill at (49,29). Clip each fill horizontally by current/max. HP max = 100.
Draw slots over fills. Attack (18,7), sound (7,18), block or empty (29,18), dash (18,29).
Name area (57,9) to (140,16). Draw labels in game.




Cooldown: draw cooldown_overlay.png (21 x 21) at the same position as the slot. Black alpha 145/255. Clip vertically for progress.

Angled fill: translate the full right-edge mask left by round((1-fraction)*travel), and intersect it with the original fill alpha. For each row keep pixels x < fullRightExclusive[row] - shift. At zero hide fill entirely. Use the top row filled width as travel.

Mana track: draw mana_track.png at (49,29) under mana_fill. Leave track visible at zero.
