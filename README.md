# Vlliards

## PC prototype (current work)

Open `Assets/Scenes/DesktopGame.unity` and press Play. If the scene has not been generated yet, use **Vlliards > Desktop > Create PC Scene** once, then open it.

This is an offline real-time match against a CPU, viewed from your cue ball. It uses the original table cloth/frame, numbered ball meshes, lounge architecture/furniture, and music. You control the white cue ball; the CPU controls its own cue ball (identified by the CPU label and cyan radar marker). There are no turns: both may launch whenever their independent cooldowns expire.

- Each launch has a 3-second cooldown. These values can be changed on the DesktopGame component.
- The lowest numbered remaining object ball at the start of a shot must be contacted first.
- Missing every object ball, contacting the wrong ball first, scratching, or failing to pocket a ball or reach a cushion after first contact is a foul.
- A foul freezes the offender's cue ball in place for 5 seconds, disabling movement and launches. There is no ball-in-hand placement or time-score penalty. A scratched cue ball is automatically returned to a free location.
- A legally pocketed 9 wins once that shot settles. An illegally pocketed 9 is respotted.
- Simultaneous shots are tracked independently. Object-ball credit follows the cue-ball hit and then the stronger incoming momentum through object-ball collisions. Another player's impact may change that credit.
- The HUD shows both cooldowns, freeze timers, and the next target. The CPU uses the same launch and foul rules as the human player.

- WASD: move the white ball relative to the direction you are looking.
- Mouse: look around and aim. Hold the left button to charge, then release to launch horizontally in your viewing direction. The camera follows the ball without rolling.
- Space: launch at fixed power.
- R: restart. Escape: pause/resume and release/capture the cursor. Losing focus pauses the match.
- The table radar shows balls behind you; world labels identify visible numbered balls.

Game code lives in `Assets/Scripts/Desktop`. The only game scene is `Assets/Scenes/DesktopGame.unity`; scene setup and rule checks live in `Assets/Editor`. Executable builds are not part of the current workflow; verify in Unity Play mode.


The reusable lounge prefab is `Assets/Environment/LoungeEnvironment.prefab`, generated from the preserved `BaLSample.unity` artist scene via **Vlliards > Desktop > Prepare Original Lounge**. The artist source scenes and materials remain intact.

The table and all ball meshes/materials come from `Assets/Modeling/Billiards.glb`, including its original numbered texture atlas. No ball material tint is applied. Gameplay cushion faces use the measured inner edges of the original table. Decorative shell colliders are disabled after measurement so they cannot seal the pocket entrances. All six pocket centres match the circular cut-outs in the original cloth. Captured balls sink for 0.28 seconds before disappearing; scratched cue balls return after the sink animation and remain frozen for the rest of the five-second penalty.

Pocket regression checks cover 54 physically simulated trajectories across all six holes (three speeds and three approach offsets), plus six Play-mode cue-ball-to-object-ball shots that verify collision credit and sink animation.

Shot intent: holding LMB draws back the original STICK1 cue with animated gloved hands; release shows a quick thrust and follow-through. The CPU visibly aims for 0.9 seconds before shooting, with a charge ring, direction guide, and AIMING label. It commits to its direction for the final 0.22 seconds. The normal 3-second cooldown starts when the shot fires; fouls cancel the preparation. Pause freezes the motion, and restart clears it. The wind-up duration is configurable on DesktopGame. All cue/hand visuals are non-colliding and do not affect ball physics.
