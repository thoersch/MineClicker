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
