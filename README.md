# Idle Mine: Unity project

A portrait mobile "upgrade idle" game. You dig down through an endless layered mine, assign miners to layers, and sell ore for cash. Cash buys nodes in a 538-node skill tree, starting with **Add 1 Miner**.

## Open and play

1. **Unity Hub → Add → Add project from disk**, then pick this `IdleMine` folder.
   * The project targets **Unity 2022.3 LTS**.
   * Unity 6 also works: accept the upgrade prompt.
2. Open `Assets/IdleMine/Scenes/Main.unity`. It is also scene 0 in Build Settings.
3. In the Game view, pick a portrait resolution. Add **1080 x 1920** if it isn't listed.
4. Press **Play**. Tap a layer to dig, then open the Skill Tree and buy *Add 1 Miner*.

## Folder layout

```
Assets/IdleMine/
  Scenes/Main.unity        the whole game UI, laid out in the editor
  Prefabs/                 things spawned at runtime
    LayerRow               one row of the mine (panel, breakthrough bar, 8 miner slots, +/- controls)
    SkillNode              one circle in the skill tree
    SkillEdge, BranchLabel tree connectors and branch titles
    FloatingText, OreChip  tap feedback ("+$1.2K", flying ore bits)
  Config/GameConfig.asset  all balance numbers (select it and edit in the Inspector)
  Art/Sprites/             Rounded (9-sliced), Circle, Ring
  Art/Fonts/               Lilita One (SIL Open Font License)
  Scripts/Core/            GameManager, GameConfig, stats, layers, save system, number formatting
  Scripts/SkillTree/       tree data + procedural generator
  Scripts/UI/              one view script per piece of UI
```

## Scene hierarchy

```
Main Camera
EventSystem                  StandaloneInputModule, drag threshold 12
GameManager                  GameManager (Config -> GameConfig.asset)
Canvas                       Screen Space Overlay, scales from 1080x1920 (match width)
  Background
  SafeArea                   keeps UI clear of notches
    Mine                     ScrollRect + MineView (pools LayerRow prefabs below the Surface art)
    Hud                      HudView: cash, cash/sec, ore, miners, depth
    BottomBar                Auto Assign + Skill Tree buttons, affordable badge
    Fx                       FxLayer: floating numbers and ore chips
    Toasts                   "DEPTH 12 REACHED!" banner
    SkillTree   (inactive)   SkillTreeView: pan/zoom viewport, header, details sheet
    OfflinePopup             "Welcome back" card (its Dim child starts inactive)
```

## How it's wired

The pieces connect through Inspector references, not code lookups:

* **GameManager** runs everything. It initialises first (`DefaultExecutionOrder(-100)`), loads the save, applies offline earnings, and raises events such as `LayerUnlocked`, `NodePurchased` and `Tapped`.
* **View scripts** (`HudView`, `MineView`, `BottomBar`, and so on) each hold a `game` reference plus references to their own Texts, Images and child objects. To restyle anything, move it or recolour it in the scene. The code only reads and writes the values it needs.
* **Buttons** use normal Inspector `OnClick` events:

  | Button | Calls |
  |---|---|
  | Auto Assign | `GameManager.AutoAssign` |
  | Skill Tree | `SkillTreeView.Open` |
  | X | `SkillTreeView.Close` |
  | Next Best | `SkillTreeView.FocusNextBest` |
  | Buy | `SkillTreeView.Buy` |
  | Go Deepest | `MineView.ScrollToDeepest` |
  | Collect | `OfflinePopup.Collect` |

* **Row and node buttons** are hooked up in code, because each instance acts on its own layer or node.
* **Prefab links:** MineView uses the `LayerRow` prefab, SkillTreeView uses the `SkillNode` / `SkillEdge` / `BranchLabel` prefabs, and FxLayer uses the `FloatingText` / `OreChip` prefabs. Edit a prefab and every instance changes.
* **Lazy tree build:** the skill tree panel starts inactive. It instantiates its ~540 nodes the first time it opens, which causes a short hitch on that first open only.

## Tuning

Select `Config/GameConfig.asset`. Most fields have tooltips. The main levers are:

* **Layers:** `baseOreRequired` / `oreRequiredGrowth` set how fast layers get harder. `oreValueGrowth` sets how fast deeper ore pays more.
* **Skill costs:** `nodeBaseCost` / `nodeCostGrowth` set the price of each tier.
* **Offline:** `baseOfflineEfficiency` / `baseOfflineCapHours` control offline earnings.
* **Tree size:** `tiersPerBranch`, `treeInnerRadius` and `treeTierSpacing` control the layout.

With the default config, a simulated player who always buys the cheapest affordable node reaches roughly:

| Time played | Active (4 taps/s) | Casual (0.5 taps/s) |
|---|---|---|
| First purchase | 4 s | 30 s |
| 10 min | 103 nodes, depth 17 | 42 nodes, depth 7 |
| 1 hour | 171 nodes, depth 26 | 126 nodes, depth 19 |

In the longer-horizon balance model, a player reaches about 275 nodes after a day and about 305 after two, so the full tree lasts a long time.

## Changing content

* **Skill nodes:** `SkillTree/SkillTreeGenerator.cs` builds the tree. `BranchNames`, `BranchColors` and `KeystoneNames` define the 6 branches. `SpineEffects`, `SideEffects` and `KeystoneEffects` decide what each branch's nodes do.
  * Node ids are stable, so saves survive re-tuning.
  * Adding tiers or branches only adds nodes.
* **New stats:**
  1. Add a value to `StatType` in `Core/Stats.cs`.
  2. Give it a base value with `Stats.SetBase` in `GameManager.Init`.
  3. Read it where it matters in `GameManager`.
  4. Grant it from one of the generator's effect methods.
* **Art:** swap the sprites in `Art/Sprites`, or assign your own art on the prefabs and scene objects. The miner figure in `LayerRow` is built from simple Images, so replacing it with a sprite or animation only needs the `figures` list on `LayerRowView` to point at the new objects.
* **Text:** the project uses built-in uGUI `Text` so it works with no extra imports. To move to TextMeshPro, swap components and change the `Text` fields to `TMP_Text`.

## Input

The project uses the classic Input Manager with `StandaloneInputModule`. All gameplay input goes through EventSystem pointer events: taps, the scroll view, and pinch/pan in the skill tree. To switch to the new Input System package:

1. Install the package.
2. Set *Active Input Handling* to **Input System** or **Both**.
3. Click *Replace with InputSystemUIInputModule* on the EventSystem.

The only direct `Input` call is the Android back button closing the tree, and it compiles out automatically when the legacy input handler is disabled.

## Paragon

**One level per ascension.** Progress toward the next Paragon level counts only the money earned this run. Ascending banks exactly one level and resets that count, so anything earned past the requirement is lost. This encourages players to ascend as soon as they can. The tuning is `paragonBaseRequirement` / `paragonRequirementGrowth` in GameConfig.

**The celebration.** Ascending plays a full-screen celebration: spinning rays, the new level, the multiplier jump, confetti, the fanfare and heavy haptics. Tap anywhere to continue.

**The Paragon tree** (`SkillTree/ParagonTree.cs`, `UI/ParagonTreeView.cs`):
* Open it with the purple PARAGON TREE button in the skill tree. It turns over like a card.
* You get one Paragon Point per Paragon level. Every perk costs 1 point.
* There are three lanes: Crew, Riches and Depths. Tier N of each lane unlocks at Paragon N and needs the perk above it, so every new level always has something to spend on.
* Every fifth tier is a major perk (x2 Miner Speed, x2 Ore Value, x2 Dig Speed or x3 Tap Power, plus a couple of extra miner slots).
* The tree is endless. It is generated tier by tier and always reaches 10 tiers past your level.
* Perks are kept when you ascend. RESPEC (tap twice) refunds every point for free.
* Tune the lanes in `ParagonTree.Minor()` / `Major()`. Perk ids are `pa`/`pb`/`pc` plus the tier. Perks bought in the old 15-perk tree are refunded on load.

**Haptics.** `Feedback.Play(Sfx.X)` plays a sound together with a matching haptic, from a selection tick on button presses up to heavy hits for keystones, Motherlodes and ascending. The iOS side is `Assets/Plugins/iOS/IdleMineHaptics.mm`. Players can turn haptics off in Settings.

**Install or update all UI:** open `Main.unity`, click **Idle Mine > Install All UI In Open Scene**, then save the scene. This runs the monetization, audio and Paragon installers together. Pieces that already exist are left alone.

## The Deep Core (prestige tree)

**Unlocking it.** The first time a run buys all 538 skills, the Deep Core unlocks permanently, with its own full-screen celebration.

**The tree.** It's a second tree of 537 nodes: same radial layout, molten theme, hexagon nodes (`SkillTree/DeepCoreRecipe.cs`).
* Like the skill tree, it resets every run and costs cash.
* Its first node, Breach the Core, needs that run's skill tree complete.
* Open it with the DEEP CORE button on the skill tree; the panel flips over like a card.

**The five mechanics.** Each branch's first node switches one on, and everything after it in that branch makes it stronger:

| Branch | Mechanic | How it plays |
|---|---|---|
| Demolition | Dynamite | Hold a layer to light the fuse: the whole row becomes a burning timer racing toward a TNT stick (so your thumb never hides it). Release to blast. Worth about 25 s of income at full charge, spread over nearby layers. Has a cooldown. |
| Gemcraft | Gem veins | Glowing gems appear in the mine. Tap one before it fades for minutes of income. |
| Drillworks | Drill rigs | Every drill works the deepest layer, without using miner slots. They stack: each one digs like extra miners and adds to the breakthrough speed (x3 with one drill, x5 with two, raised by Bore Speed). Overclock boosts both. The layer's bar turns orange and striped and shows the speed-up and the time saved. Drills drive depth; auto-tap stays a cash source. |
| Overdrive | Overclock | A gauge fills as you play. When full, tap it for a burst of extra production. |
| Timing | Power swings | A bar appears with a marker sweeping past a gold zone. Tap in the gold for a big strike, worth about 60 s of income. |

The sixth branch, Core, adds raw multipliers.

**BUY ALL.** Once the Deep Core is unlocked, the skill tree gets a BUY ALL button that buys every affordable skill, cheapest first.

**Tuning.** Costs and mechanic settings are under **Deep Core** in GameConfig. Edit the node effects in `DeepCoreRecipe`. Both trees share `SkillTreeGenerator`'s layout through `TreeRecipe`.

**Install:** run **Idle Mine > Install All UI In Open Scene** (or **Idle Mine > Deep Core > Install In Open Scene**), then save the scene.

## Ads & purchases

Rewarded ads only, always opt-in: no banners and no forced interstitials. Each ad button shows an "AD" tag so a player knows before tapping that a video will play. A button hides itself when no ad is loaded, when a cooldown is running, or once the daily cap is reached.

| Placement | Where | Reward |
|---|---|---|
| `OfflineDouble` | "Welcome back" popup: COLLECT ×2 | The offline earnings again |
| `IncomeBoost` | HUD, top right | ×2 cash for 30 min, stacking to 4 h. It keeps running while the player is away. |
| `OreCart` | Rolls across the mine every 4 to 8 min | Tap for 2 min of income, or watch an ad for 15 min of income |
| `MotherlodeChest` | Appears after reaching a Motherlode | 30 min of income |
| `SkillAssist` | Skill tree details sheet, when you have at least 70% of a skill's cost | Covers the rest and buys the skill (5 min cooldown) |

Rewards are sized as "minutes of income", so they stay meaningful at every stage. New players see no ads for their first 6 minutes of play, and there's a cap of 25 ads per day. Every number is in `GameConfig.asset` under **Ads**.

The **Foreman Pass** is a one-time purchase. It makes every ad reward instant and adds +50% offline earnings. Players reach it from the PASS button on the HUD (hidden once owned) or the "No ads?" link on any offer. The entitlement is cached in its own file (`idlemine_monetization.json`), so wiping or corrupting the game save never removes something the player paid for.

**Install the UI once:** open `Main.unity`, click **Idle Mine > Monetization > Install UI In Open Scene**, then save the scene. The menu item adds the `AdManager` object, the offer and pass popups, the HUD boost and PASS buttons, the cart/chest visitor, the skill assist button and the ×2 offline button. It also wires every reference. After that it's ordinary scene UI you can restyle.

**How it's wired:**

* `Monetization/AdManager` is the single gatekeeper. Views call `CanOffer(placement)` and `ShowRewarded(placement, ok => ...)`.
  * It enforces the grace period, the daily cap and cooldowns.
  * It skips the video for pass owners.
  * It tells `GameManager` to ignore the app-pause that an ad causes. Otherwise a 30 s ad would return to a "Welcome back" popup.
* The ad network and store sit behind `IRewardedAdService` / `IPurchaseService`. Which ones run:
  * **Editor and development builds:** a fake full-screen **TEST AD** with a countdown, and a store where every purchase succeeds. Lower *Mock Fill Rate* on AdManager to test the "no ad available" path.
  * **Release builds:** real SDKs once their scripting defines are set (see below), otherwise none at all. Without an SDK, no ad buttons appear.

**Turning on the real SDKs (iOS):**

`Packages/manifest.json` already includes **Google Mobile Ads** 11.5.0 (from OpenUPM) and **Unity IAP** 5.4.3.

1. Click **Idle Mine > Monetization > Set Up iOS SDKs**. It:
   * adds the `IDLEMINE_ADMOB;IDLEMINE_UNITY_IAP` scripting defines for iOS;
   * fills in the AdMob iOS app id (Google's test id until you replace it) and the App Tracking Transparency prompt text in *Assets > Google Mobile Ads > Settings*.
2. Put your real AdMob **app id** in *Assets > Google Mobile Ads > Settings*, and your rewarded **ad unit** id in the iOS field on the `AdManager` object. Until then, both are Google's test ids, which always fill and never pay.
3. Create a non-consumable in-app purchase with id `foreman_pass_iap` (configurable in GameConfig) in App Store Connect.
4. In the AdMob console, create the GDPR consent message and the iOS IDFA (ATT) explainer under *Privacy & messaging*. The game shows Google's consent form before the first ad request when it's required.
5. Fill in your publisher id in `app-ads.txt` (project root) and upload it to the root of the developer website on your App Store listing.

The Google plugin adds Apple's SKAdNetwork ids to the Xcode project automatically. Building for iOS needs a Mac with Xcode, or a cloud build service.

## Audio

**Install once:** open `Main.unity`, click **Idle Mine > Audio > Install In Open Scene**, then save the scene. The menu item adds:
* an `Audio` object holding `AudioManager` (every clip, with its volume, pitch variation and repeat limit) and `GameAudio`;
* a gear button in the HUD;
* the settings popup, with Music and Sound on/off and Restore purchases.

**How it works:**
* Anything can call `AudioManager.Play(Sfx.X)`.
* `GameAudio` turns game events into sounds:
  * a tap digs, and a crit clangs; auto-taps stay silent;
  * a new layer rumbles open, and a Motherlode shimmers;
  * a skill purchase pops, and a keystone chimes;
  * ascending swells;
  * ad and Foreman Pass rewards sparkle.
* `PressScale` clicks on every button press and plays a soft "nope" on disabled ones.
* Coins, the ore cart and the pass purchase call `Play` directly.
* Music fades in and out, and the on/off choices are remembered per device in PlayerPrefs.
* Ads pause all audio.

**The clips** in `Assets/IdleMine/Audio` were synthesized for this game, so there are no licensing strings attached. `Editor/AudioImportSettings.cs` imports them for mobile: effects are decompressed into memory, and the music streams as Vorbis. To swap a sound, replace the `.wav` with the same name, or assign new clips on the `AudioManager` component.

## iOS builds (GitHub Actions)

`.github/workflows/ios-app-store.yml` builds the game and uploads it to App Store Connect, where it appears in TestFlight. Start it from **Actions > iOS build to App Store Connect > Run workflow**, or by pushing a tag such as `v1.0.0`.

* A Linux runner exports the Xcode project with Unity (GameCI), using `Editor/CiBuild.cs`. This raises the minimum iOS version to 15.0 if it's lower, because Unity IAP 5 needs StoreKit 2.
* A macOS runner then runs `pod install`, signs the app automatically with the App Store Connect API key, and uploads it. The workflow's run number becomes the build number.
* The required repository secrets are listed at the top of the workflow file.

## Debugging

* Right-click the **GameManager** component header in Play mode for:
  * **Debug/Add 1000x current money**
  * **Debug/Wipe save and restart** (also wipes ad caps and the cached Foreman Pass)
* Right-click the **AdManager** component header for:
  * **Debug/Toggle Foreman Pass**
  * **Debug/Reset ad caps and cooldowns**
* In the editor, *Ignore Grace And Cap In Editor* (on AdManager, on by default) lets you test ads immediately. Turn it off to see what a new player sees.
* The save file is `idlemine_save.json` in `Application.persistentDataPath`. Ad and purchase state is in `idlemine_monetization.json` next to it.

## Notes

* The scene, prefabs and meta files were generated as Unity text YAML and checked by script. The checks cover:
  * every reference resolves;
  * every serialized field name matches its C# class;
  * every referenced object has the right type;
  * every Inspector button calls a real public method;
  * the scripts compile against the Unity API.

  They have not yet been opened in the editor. On first import Unity will rewrite some files in its own formatting and fill in any default settings that aren't listed. That is expected and harmless.
* `ProjectSettings` sets:
  * portrait orientation;
  * the product name;
  * a 540x960 windowed desktop build for quick testing;
  * the classic input handler.

  Everything else is Unity's defaults.
* Font: Lilita One by Juan Montoreano, SIL Open Font License 1.1 (`Art/Fonts/LilitaOne-OFL.txt`).
