# Base iOS and web compared with Android 1.0.2

Reference: `iQuarters-android/iquarters-src/app/src/main/java/com/guille/spring/iquarters/scripts` (49 scripts) and its `IqPhysics.kt`/`IqRenderer.kt`. This is a work ledger, not a 1:1 certification. Base iOS and base web share the C# game/session/presentation code and recovered assets; their rendering and platform adapters differ. Plus, Realism, and Dank are outside this scope.

| Reference scripts | Current iOS/web status | Remaining difference or verification |
|---|---|---|
| `QuarterTrigger`, `GameManagerScript`, `IqData` | Original rounds, player/shot scoring, secret sequence, flick controls, save/resume, effect ordering and replay states recovered. | Android's physics solver and scene update order differ. Need matched shot/contact fixtures and device comparison before claiming equivalent gameplay. |
| `mainmenu`, `Logo`, `About`, `AreYouSure`, `BackClearButtons`, `GameOrRoundButtons`, `GameHighScreen`, `RoundHighScreen`, `HiScoreScript`, `HiScoreRoundScript` | Recovered menu art/clip transitions, score tables, clear prompt, resume prompt and About text used. | Host text font and website-link behavior remain different; compare touch targets and transitions on physical devices. |
| `PauseButtonScript`, `PauseMenu`, `Help`, `ShotTypeHelper`, `AngleAdjustScript`, `InGameAngleIcon`, `UIPlayer`, `RoundIndicator`, `CoinHolder` | Original clips, two help pages, shot-type prompt, angle control, player banner, rotating round number and shot HUD hiding implemented. World prop clips continue under the pause menu. | Check transitions and animation phase against device recordings. |
| `GlassScaleController`, `lightray`, `PowerX`, `Streak`, `RicochetExciter`, `RoundComplete`, `GameOver`, `PracticeGreatScore`, `SecretRound`, `CoinsLeft`, `AnnouncerScript`, `CrowdScript` | Recovered textures/sounds and major effect sequences; contact flash, multiplier placement, coin stack, and 1.25-second ricochet award included. | Sound mixing and frame timing require side-by-side recording. Ricochet display can overlap the next transition as in the reference, but exact ordering needs capture comparison. |
| `ReplayController`, `ReplayCameraScript`, `SaveReplayButtons` | Automatic and requested current-shot replay, camera exceptions, contact audio/flash, skip, and requested-replay Done screen implemented. | Android re-simulates its recorded shot; iOS/web play saved per-frame poses. This can visibly diverge for moving props and skipped replay timing. `SaveReplayButtons` is effectively inert in the Android reference. |
| `BirdScript`, `LighterScript`, `LauncherScript`, `LazySusanGlassShadow`, `ShadowQuarterScript`, `MainCameraScript`, `IntroCamScript`, `SpotlightScript`, `spotdirScript` | Recovered prop clips/reactions, level camera, coin shadow, and lazy-Susan glass shadow used. Self-targeted animation curves for the Susan and light ray now resolve. | Animated spotlight direction and exact prop state through replay need comparison. `IntroCamScript` and `spotdirScript` appear unbound/inactive in the shipped scene. |
| `PracticeUI`, `StatsScreen`, `InGameHiScore` | Practice selector/lock, four statistic rows, name entry and score board implemented with recovered art/animations. | Native keyboard and bitmap font layout remain platform-specific. |

## Verified in this working update

- Shared C# core and presentation files match between iOS and web; platform adapters differ intentionally.
- Core verification exercises all 13 round collision sets, original replay-camera selection, camera reset, and flick sampling at 30–240 Hz.
- Presentation verification exercises one/two/four-player end states, scoring and remaining-coin bonus, both help pages, pause-time prop motion, practice, secret round, HUD, contact flash and shadow.
- Web release publish completed and loaded locally without runtime errors. iOS build 12 completed as an ad-hoc-signed local IPA for iOS 15+; physical-device launch and rendering remain untested.

## Highest-priority gaps before a 1:1 claim

1. Port or cross-check Android's contact manifold, solver, inertia and sleep rules against matching shot traces. The present C# solver uses 0.004-second substeps and triangle contacts; Android uses a 0.02-second sequential-impulse simulation. Identical scripts and assets alone cannot make trajectories identical.
2. Record comparable clips on Android/iOS/web for round start, scoring, ricochet replay, pause and final statistics; inspect frame timing, camera pose, shader output and sounds.
3. Bring the bitmap font/UI to the reference where side-by-side comparison shows a mismatch.
