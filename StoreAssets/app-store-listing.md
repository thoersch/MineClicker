# Deepforge: App Store Connect listing (iOS 1.0)

Copy each field into App Store Connect. Character limits are in brackets.

## App Information

| Field | Value |
|---|---|
| Name [30] | Deepforge: Idle Miner |
| Subtitle [30] | Dig deep. Upgrade everything. |
| Primary category | Games, then **Simulation** and **Casual** |
| Content rights | Yes, contains third-party content (ads), and you have the rights |
| Age rating | Answer None/No to every content question, so it should be 4+. Not "Made for Kids". |

## Version 1.0.0

**Promotional Text [170]**

> Dig through endless layers of rock, hire miners, and grow a 538-skill upgrade tree. Your mine keeps earning while you're away. Come back to a fortune.

**Description [4000]**

> Grab a pickaxe and start digging. Deepforge is a relaxing idle mining game where every tap, every miner and every upgrade takes you deeper.
>
> DIG DEEPER
> Break through layer after layer of an endless mine, from topsoil to granite, basalt and beyond. Every layer has its own ore, and deeper ore is worth more. Watch for rich veins and Motherlodes worth three times as much.
>
> BUILD YOUR CREW
> Hire miners and put them to work where they earn the most, or let Auto Assign do it for you. Unlock more slots and watch your crew swing away.
>
> GROW A HUGE SKILL TREE
> Spend your cash on a 538-skill tree with six branches: miner speed, ore value, tap power, dig speed, offline earnings and more. Unlock keystones for big permanent jumps.
>
> EARN WHILE YOU'RE AWAY
> Your miners keep digging when you close the game. Come back to a pile of cash waiting for you.
>
> ASCEND
> Reset your mine for permanent Paragon levels that make every future run faster.
>
> PLAY YOUR WAY
> Ads are always optional: watch one only if you want a boost, a bigger ore cart haul or double offline earnings. Or get the one-time Foreman Pass to skip every ad and earn 50% more offline.

**Keywords [100]**

```
idle,mining,miner,clicker,incremental,tycoon,upgrade,skill tree,prestige,dig,ore,offline,gold,cave
```

(98 characters. Don't repeat words from the name or subtitle; Apple already indexes those.)

| Field | Value |
|---|---|
| Support URL | A page on https://tenx.llc with a contact email (required) |
| Marketing URL | https://tenx.llc (AdMob finds app-ads.txt through this domain) |
| Version | 1.0.0 (must match Player Settings > Version) |
| Copyright | 2026 [your legal company name] |

**Screenshots**

* iPhone 6.9": `StoreAssets/screenshots-iphone-6.9/` (1320×2868), upload all 5 in order
* iPad 13": `StoreAssets/screenshots-ipad-13/` (2064×2752), upload all 5 in order

**In-App Purchases and Subscriptions:** add `foreman_pass_iap` to this version.

## App Review Information

| Field | Value |
|---|---|
| Sign-in required | No |
| Contact | Your name, phone and email |

**Notes:**

> No account or sign-in is needed. Ads are optional rewarded videos, and nothing in the game requires watching one. The Foreman Pass (foreman_pass_iap) is available from the blue PASS button at the top right of the main screen. Restore Purchases is in the same popup.

## In-App Purchase: foreman_pass_iap

| Field | Value |
|---|---|
| Type | Non-Consumable |
| Reference name | Foreman Pass |
| Product ID | foreman_pass_iap |
| Display name [35] | Foreman Pass |
| Description [55] | Skip every ad and earn 50% more while offline. |
| Review screenshot | `StoreAssets/iphone_review_foreman_pass.png` |
| Review notes | Opened from the blue PASS button on the main screen. |
