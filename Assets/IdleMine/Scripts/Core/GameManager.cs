using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleMine
{
    public struct TapResult
    {
        public int Layer;
        public double Ore;
        public double Money;
        public bool Crit;
        public bool Auto;
    }

    public class BlastResult
    {
        public bool Detonated;
        public int Center;
        public float Charge;
        public double Money;
        public readonly List<int> Layers = new List<int>();
    }

    public class OfflineReport
    {
        public double AwaySeconds;
        public double SimulatedSeconds;
        public double Money;
        public int LayersGained;
    }

    /// <summary>
    /// The whole game simulation. No UI code in here: views read state and subscribe to events.
    ///
    /// Loop:  miners (and taps) dig ore on their layer
    ///        -> ore sells instantly for cash (ore x layer value)
    ///        -> ore also fills the layer's breakthrough bar; a full bar on the deepest layer opens the next
    ///        -> cash buys skill-tree nodes -> nodes change stats -> everything digs faster/earns more.
    /// </summary>
    [DefaultExecutionOrder(-100)] // initialise before any view's Awake/Start
    public class GameManager : MonoBehaviour
    {
        [Tooltip("Balance settings. Leave empty to run on GameConfig defaults.")]
        [SerializeField] GameConfig config;
        [SerializeField] int targetFrameRate = 60;
        [SerializeField] bool keepScreenAwake = true;

        public static GameManager Instance { get; private set; }

        public GameConfig Config { get; private set; }
        public StatBlock Stats { get; private set; }
        public SkillTree Tree { get; private set; }
        public readonly List<MineLayer> Layers = new List<MineLayer>();

        public double Money { get; private set; }
        public double LifetimeOre { get; private set; }
        public double LifetimeMoney { get; private set; }
        public long TotalTaps { get; private set; }
        public double IncomePerSecond { get; private set; }
        public int AssignedMiners { get; private set; }
        public OfflineReport PendingOfflineReport { get; set; }

        /// <summary>Permanent prestige level. Never reset by Ascend; only ever goes up.</summary>
        public int ParagonLevel { get; private set; }

        /// <summary>Permanent multiplier from past ascensions, applied to Ore Value, Miner Speed, Tap Power
        /// and Dig Speed every time stats are recalculated (see RecalculateStats).</summary>
        public double ParagonMultiplier { get { return Math.Pow(1.0 + Config.paragonMultiplierPerLevel, ParagonLevel); } }

        /// <summary>Cash earned since the last ascension (or new game). Paragon progress counts only this run,
        /// and ascending resets it, so earnings past the next level's cost are lost: ascend when you can.</summary>
        public double RunMoney { get { return _runMoney; } }

        /// <summary>Run earnings needed for the next Paragon level. Each level costs paragonRequirementGrowth
        /// times more than the last.</summary>
        public double NextParagonCost { get { return ParagonLevelCost(ParagonLevel); } }

        /// <summary>One level at a time: 1 when this run has earned enough for the next level, else 0.</summary>
        public int AvailableParagonLevels { get { return CanAscend ? 1 : 0; } }

        public bool CanAscend { get { return _runMoney >= NextParagonCost; } }

        /// <summary>The Deep Core: the prestige tree past the skill tree (resets every run, like the skill tree).</summary>
        public SkillTree DeepTree { get; private set; }

        /// <summary>Permanent: set the first time a run completes the whole skill tree. Never reset.</summary>
        public bool DeepCoreUnlocked { get; private set; }

        /// <summary>The Paragon tree's perks (bought with Paragon Points, kept across ascensions, free to respec).</summary>
        public ParagonTree ParagonTree { get; private set; }

        /// <summary>Paragon Points: one per Paragon level. Ascending adds one; perks stay bought.</summary>
        public int ParagonPoints { get { return ParagonLevel; } }
        public int ParagonPointsSpent { get; private set; }
        public int ParagonPointsAvailable { get { return Math.Max(0, ParagonPoints - ParagonPointsSpent); } }

        /// <summary>UTC ticks when the ad-bought income boost runs out (0 = never bought). It's wall-clock
        /// time, so a boost keeps running (and boosting offline earnings) while the player is away.</summary>
        public long BoostEndUtcTicks { get; private set; }
        public bool BoostActive { get { return _boostApplied; } }
        public double BoostSecondsLeft { get { return Math.Max(0, (BoostEndUtcTicks - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond); } }
        public bool CanExtendBoost { get { return BoostSecondsLeft + Config.boostMinutesPerAd * 60.0 <= Config.boostMaxHours * 3600.0 + 1.0; } }

        /// <summary>Income without the temporary boost. Ad rewards are sized from this so a boost doesn't double them too.</summary>
        public double BaseIncomePerSecond { get { return IncomePerSecond / BoostFactor; } }

        /// <summary>Owns the one-time Foreman Pass purchase (cached locally by MonetizationStore).</summary>
        public bool ForemanPass { get; private set; }

        /// <summary>Set while a full-screen ad is up. Mobile OSes pause the app behind an ad, and a 30 s ad
        /// would otherwise come back to a "Welcome back" popup.</summary>
        public bool SuppressOfflineProgress { get; set; }

        public int TotalMiners { get { return (int)Math.Floor(Stats.Get(StatType.MinerCount) + 1e-6); } }
        public int FreeMiners { get { return Math.Max(0, TotalMiners - AssignedMiners); } }
        public int SlotsPerLayer { get { return (int)Math.Floor(Stats.Get(StatType.LayerSlots) + 1e-6); } }
        public MineLayer Frontier { get { return Layers[Layers.Count - 1]; } }
        public int Depth { get { return Layers.Count - 1; } }

        // Events for views. Keep handlers light, they can fire many times per frame.
        public event Action<MineLayer> LayerUnlocked;
        public event Action<MineLayer> LayerCleared;
        public event Action<TapResult> Tapped;
        public event Action<SkillNode> NodePurchased;
        public event Action MinersChanged;
        public event Action<int> Ascended; // arg = Paragon levels gained
        public event Action<ParagonPerk> PerkPurchased;
        public event Action PerksRespecced;
        public event Action<MineLayer> DrilledThrough;  // the drills broke through a layer (before the next one opens)
        public event Action DeepCoreOpened;          // first time the skill tree is completed
        public event Action<BlastResult> Blasted;     // a dynamite blast went off
        public event Action OverclockStarted;

        double _digSpeed, _critChance, _critMult, _autoTapRate;
        double _autoTapAccum;
        float _saveTimer;
        bool _initialized, _boostApplied;
        double _runMoney;
        float _dynamiteReadyAt, _overclockEndsAt;
        double _overclockMeter, _overclockPowerAtStart;
        bool _overclockApplied;
        readonly HashSet<ParagonPerk> _ownedPerks = new HashSet<ParagonPerk>();
        long _pausedAtTicks;
        readonly List<int> _order = new List<int>();

        // ================================================================== setup

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Application.targetFrameRate = targetFrameRate;
            if (keepScreenAwake) Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (config == null)
            {
                Debug.LogWarning("[IdleMine] No GameConfig assigned on GameManager, using defaults.");
                config = ScriptableObject.CreateInstance<GameConfig>();
            }
            Init(config);
        }

        /// <summary>Builds the tree, loads the save and applies offline progress. Called from Awake;
        /// public so tests or a custom loader can drive it directly.</summary>
        public void Init(GameConfig cfg)
        {
            Instance = this;
            Config = cfg;
            Stats = new StatBlock();
            Tree = SkillTreeGenerator.Generate(cfg);
            DeepTree = SkillTreeGenerator.Generate(cfg, new DeepCoreRecipe());
            ParagonTree = ParagonTree.Build();
            ForemanPass = MonetizationStore.Data.foremanPass;

            var save = SaveSystem.Load();
            if (save != null) ApplySave(save);
            else NewGame();

            GrowParagonTree();
            _initialized = true;
            if (save != null) SimulateOffline(save.lastSaveUtcTicks);
            SyncBoost();
        }

        /// <summary>Resets money, miners and the skill tree back to a fresh start. Used both for a brand new save
        /// (which also clears Paragon perks) and for Ascend, which keeps them.</summary>
        void NewGame(bool keepPerks = false)
        {
            Money = Config.startingMoney;
            _runMoney = 0;
            AssignedMiners = 0;
            Tree.ResetOwnership();
            DeepTree.ResetOwnership();
            if (!keepPerks)
            {
                _ownedPerks.Clear();
                ParagonPointsSpent = 0;
            }
            Layers.Clear();
            Layers.Add(new MineLayer(0));
            RecalculateStats();
            PlaceFreeMiners();
        }

        void ApplyBaseStats()
        {
            Stats.SetBase(StatType.MinerCount, Config.startingMiners);
            Stats.SetBase(StatType.MinerSpeed, 1);
            Stats.SetBase(StatType.OreValue, 1);
            Stats.SetBase(StatType.TapPower, 1);
            Stats.SetBase(StatType.LayerSlots, Config.baseSlotsPerLayer);
            Stats.SetBase(StatType.DigSpeed, 1);
            Stats.SetBase(StatType.CritChance, Config.baseCritChance);
            Stats.SetBase(StatType.CritMultiplier, Config.baseCritMultiplier);
            Stats.SetBase(StatType.AutoTapRate, 0);
            Stats.SetBase(StatType.DepthBonus, 0);
            Stats.SetBase(StatType.OfflineEfficiency, Config.baseOfflineEfficiency);
            Stats.SetBase(StatType.OfflineCapHours, Config.baseOfflineCapHours);
            Stats.SetBase(StatType.SkillDiscount, 0);
            foreach (StatType t in new[] { StatType.DynamitePower, StatType.DynamiteRadius, StatType.DynamiteCooldownCut,
                                            StatType.GemRate, StatType.GemValue, StatType.DrillCount, StatType.DrillPower,
                                            StatType.OverclockPower, StatType.OverclockDuration, StatType.SwingPower,
                                            StatType.SwingWindow, StatType.SwingRate, StatType.DrillBore })
                Stats.SetBase(t, 0);
            Stats.SetBase(StatType.DynamiteCharge, 1);
            Stats.SetBase(StatType.OverclockCharge, 1);
        }

        // ================================================================== frame loop

        void Update()
        {
            if (!_initialized) return;
            CheckDeepCoreUnlock();
            SyncBoost();
            UpdateOverclock(Time.unscaledDeltaTime);
            Tick(Time.deltaTime, true);

            _saveTimer += Time.unscaledDeltaTime;
            if (_saveTimer >= Config.autosaveSeconds) { _saveTimer = 0; Save(); }
        }

        /// <summary>Advance the simulation by dt seconds. Also used (without auto-taps) for offline catch-up.</summary>
        public void Tick(double dt, bool includeAutoTap)
        {
            if (dt <= 0) return;

            int count = Layers.Count; // layers opened during this tick start next tick
            for (int i = 0; i < count; i++)
            {
                var layer = Layers[i];
                double workers = layer.Miners + DrillWorkers(i);
                if (workers <= 0) continue;
                double ore = workers * layer.OrePerMinerPerSecond * Yield(layer) * dt;
                layer.PendingIncome += AddOre(layer, ore);
            }

            if (includeAutoTap && _autoTapRate > 0)
            {
                _autoTapAccum += _autoTapRate * dt;
                int taps = (int)_autoTapAccum;
                if (taps > 0)
                {
                    _autoTapAccum -= taps;
                    DoTap(Frontier, taps, true);
                }
            }
        }

        double Yield(MineLayer layer)
        {
            return layer.Cleared ? Config.clearedLayerYield : 1.0;
        }

        /// <summary>Cash per second this layer is currently producing from its miners.</summary>
        public double LayerIncome(MineLayer layer)
        {
            return (layer.Miners + DrillWorkers(layer.Index)) * layer.OrePerMinerPerSecond * layer.ValuePerOre * Yield(layer);
        }

        /// <summary>Sells ore, advances the breakthrough bar. Returns the cash earned.</summary>
        double AddOre(MineLayer layer, double ore)
        {
            if (ore <= 0) return 0;
            double cash = ore * layer.ValuePerOre;
            Money += cash;
            LifetimeMoney += cash;
            _runMoney += cash;
            LifetimeOre += ore;

            if (!layer.Cleared)
            {
                layer.Progress += ore * _digSpeed * DrillBoreFactor(layer.Index);
                if (layer.Progress >= layer.OreRequired)
                {
                    layer.Progress = layer.OreRequired;
                    if (HasDrill(layer.Index) && DrilledThrough != null) DrilledThrough(layer);
                    OnLayerCleared(layer);
                }
            }
            return cash;
        }

        void OnLayerCleared(MineLayer layer)
        {
            if (LayerCleared != null) LayerCleared(layer);
            if (layer == Frontier)
            {
                var next = new MineLayer(Layers.Count);
                Layers.Add(next);
                RecalculateLayer(next);
                PlaceFreeMiners();
                if (LayerUnlocked != null) LayerUnlocked(next);
            }
            RecalculateIncome();
        }

        // ================================================================== player actions

        /// <summary>A manual tap on a layer. Returns what it earned so the view can show it.</summary>
        public TapResult Tap(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= Layers.Count) return new TapResult { Layer = -1 };
            TotalTaps++;
            if (OverclockUnlocked && !OverclockActive) _overclockMeter = Math.Min(1.0, _overclockMeter + 0.002);
            return DoTap(Layers[layerIndex], 1, false);
        }

        TapResult DoTap(MineLayer layer, int taps, bool auto)
        {
            double ore = layer.OrePerTap * taps;
            bool crit = false;
            if (auto)
            {
                ore *= 1 + _critChance * (_critMult - 1); // auto-taps get the average crit value
            }
            else if (UnityEngine.Random.value < _critChance)
            {
                crit = true;
                ore *= _critMult;
            }

            layer.LastTapTime = Time.time;
            var r = new TapResult { Layer = layer.Index, Ore = ore, Crit = crit, Auto = auto };
            r.Money = AddOre(layer, ore);
            if (Tapped != null) Tapped(r);
            return r;
        }

        /// <summary>Move miners onto (delta &gt; 0) or off (delta &lt; 0) a layer. Returns how many moved.</summary>
        public int AssignMiners(int layerIndex, int delta)
        {
            if (layerIndex < 0 || layerIndex >= Layers.Count || delta == 0) return 0;
            var layer = Layers[layerIndex];
            int moved;
            if (delta > 0) moved = Math.Min(delta, Math.Min(FreeMiners, SlotsPerLayer - layer.Miners));
            else moved = -Math.Min(-delta, layer.Miners);
            if (moved == 0) return 0;

            layer.Miners += moved;
            AssignedMiners += moved;
            RecalculateIncome();
            if (MinersChanged != null) MinersChanged();
            return moved;
        }

        /// <summary>Pull every miner off and redistribute greedily to the best-paying slots.</summary>
        public void AutoAssign()
        {
            foreach (var l in Layers) l.Miners = 0;
            AssignedMiners = 0;
            PlaceFreeMiners();
        }

        /// <summary>Drop idle miners into the best-paying open slots. Called on new miners, slots and layers.</summary>
        public void PlaceFreeMiners()
        {
            int free = FreeMiners;
            if (free > 0)
            {
                _order.Clear();
                for (int i = 0; i < Layers.Count; i++) _order.Add(i);
                _order.Sort((a, b) => MinerWorth(Layers[b]).CompareTo(MinerWorth(Layers[a])));

                int slots = SlotsPerLayer;
                foreach (int i in _order)
                {
                    if (free <= 0) break;
                    var l = Layers[i];
                    int add = Math.Min(free, slots - l.Miners);
                    if (add <= 0) continue;
                    l.Miners += add;
                    AssignedMiners += add;
                    free -= add;
                }
            }
            RecalculateIncome();
            if (MinersChanged != null) MinersChanged();
        }

        /// <summary>Cash per second one extra miner would make here. The deepest layer gets a small nudge
        /// so miners also push the breakthrough frontier.</summary>
        public double MinerWorth(MineLayer l)
        {
            double w = l.OrePerMinerPerSecond * l.ValuePerOre * Yield(l);
            if (l == Frontier) w *= 1.05;
            return w;
        }

        public double GetCost(SkillNode n)
        {
            return Math.Ceiling(n.BaseCost * (1.0 - Stats.Get(StatType.SkillDiscount)));
        }

        public bool CanAfford(SkillNode n) { return Money >= GetCost(n); }

        /// <summary>Open for purchase in its tree. The Deep Core's first node also needs the Deep Core unlocked and
        /// this run's skill tree complete.</summary>
        public bool IsNodeAvailable(SkillNode n)
        {
            if (n == null || n.Owner == null || !n.Owner.IsAvailable(n)) return false;
            if (n.Owner == DeepTree && n == DeepTree.Root && (!DeepCoreUnlocked || !Tree.IsComplete)) return false;
            return true;
        }

        public bool CanPurchase(SkillNode n)
        {
            return IsNodeAvailable(n) && CanAfford(n);
        }

        public bool TryPurchase(SkillNode n)
        {
            if (!CanPurchase(n)) return false;
            Money -= GetCost(n);
            n.Owner.Unlock(n);

            int minersBefore = TotalMiners, slotsBefore = SlotsPerLayer;
            RecalculateStats();
            if (TotalMiners != minersBefore || SlotsPerLayer != slotsBefore) PlaceFreeMiners();

            if (NodePurchased != null) NodePurchased(n);
            CheckDeepCoreUnlock();
            return true;
        }

        /// <summary>Opens the Deep Core the first time the whole skill tree is owned. Runs after every purchase and
        /// every frame, so a save that finished the tree before the Deep Core existed still gets it (and the
        /// celebration) on its next launch.</summary>
        void CheckDeepCoreUnlock()
        {
            if (DeepCoreUnlocked || !Tree.IsComplete) return;
            DeepCoreUnlocked = true;
            Save();
            if (DeepCoreOpened != null) DeepCoreOpened();
        }

        /// <summary>BUY ALL (after the Deep Core is unlocked): buys every affordable node in the normal skill tree,
        /// cheapest first, until the money runs out. Returns how many were bought.</summary>
        public int BuyAllSkills()
        {
            int bought = 0;
            while (true)
            {
                SkillNode best = null;
                double bestCost = double.MaxValue;
                foreach (var n in Tree.Nodes)
                {
                    if (!Tree.IsAvailable(n)) continue;
                    double c = GetCost(n);
                    if (c < bestCost) { best = n; bestCost = c; }
                }
                if (best == null || !TryPurchase(best)) break;
                bought++;
            }
            return bought;
        }

        /// <summary>Banks exactly one Paragon level (one more Paragon Point), then wipes money, miners and the
        /// skill tree back to a fresh start. Paragon perks are kept. LifetimeOre/LifetimeMoney and TotalTaps are
        /// career totals and are never reset.</summary>
        public bool Ascend()
        {
            if (!CanAscend) return false;
            ParagonLevel += 1;
            GrowParagonTree();
            NewGame(keepPerks: true);
            if (Ascended != null) Ascended(1);
            Save();
            return true;
        }

        // ================================================================== Paragon tree

        /// <summary>Keeps the (endless) tree generated a few tiers past the current Paragon level.</summary>
        void GrowParagonTree() { ParagonTree.EnsureTiers(ParagonLevel + ParagonTree.TiersAhead); }

        public bool OwnsPerk(ParagonPerk p) { return _ownedPerks.Contains(p); }

        /// <summary>Paragon level reached and the perk above it owned.</summary>
        public bool IsPerkAvailable(ParagonPerk p)
        {
            if (p == null || OwnsPerk(p) || ParagonLevel < p.RequiredLevel) return false;
            foreach (var parent in p.Parents) if (!OwnsPerk(parent)) return false;
            return true;
        }

        public bool CanBuyPerk(ParagonPerk p) { return IsPerkAvailable(p) && ParagonPointsAvailable >= p.Cost; }

        public bool TryBuyPerk(ParagonPerk p)
        {
            if (!CanBuyPerk(p)) return false;
            _ownedPerks.Add(p);
            ParagonPointsSpent += p.Cost;
            int minersBefore = TotalMiners, slotsBefore = SlotsPerLayer;
            RecalculateStats();
            if (TotalMiners != minersBefore || SlotsPerLayer != slotsBefore) PlaceFreeMiners();
            if (PerkPurchased != null) PerkPurchased(p);
            return true;
        }

        /// <summary>Refunds every Paragon perk for free so the points can be spent differently.</summary>
        public bool RespecPerks()
        {
            if (_ownedPerks.Count == 0) return false;
            _ownedPerks.Clear();
            ParagonPointsSpent = 0;
            RecalculateStats();
            EnforceMinerLimits();
            PlaceFreeMiners();
            if (PerksRespecced != null) PerksRespecced();
            Save();
            return true;
        }

        // ================================================================== ad & purchase rewards

        /// <summary>Instant cash from a reward. Counts as earnings, like offline income does.</summary>
        public void GrantMoney(double amount)
        {
            if (amount <= 0) return;
            Money += amount;
            LifetimeMoney += amount;
            _runMoney += amount;
        }

        /// <summary>What N minutes of (unboosted) income is worth right now. Rewards are sized this way so they
        /// stay meaningful at every stage; before the mine earns anything it falls back to steady tapping.</summary>
        public double IncomeForMinutes(double minutes)
        {
            var top = Layers[0];
            double tapping = Config.rewardFloorTapsPerSecond * top.OrePerTap * top.ValuePerOre / BoostFactor;
            return Math.Max(BaseIncomePerSecond, tapping) * minutes * 60.0;
        }

        /// <summary>Adds boost time on top of whatever is left, capped at boostMaxHours remaining.</summary>
        public void AddBoost(double seconds)
        {
            long now = DateTime.UtcNow.Ticks;
            long end = Math.Max(now, BoostEndUtcTicks) + (long)(seconds * TimeSpan.TicksPerSecond);
            BoostEndUtcTicks = Math.Min(end, now + (long)(Config.boostMaxHours * 3600.0 * TimeSpan.TicksPerSecond));
            SyncBoost();
            Save();
        }

        public void SetForemanPass(bool owned)
        {
            if (ForemanPass == owned) return;
            ForemanPass = owned;
            RecalculateStats();
        }

        double BoostFactor { get { return _boostApplied ? Math.Max(1e-9, Config.boostMultiplier) : 1.0; } }

        void SyncBoost()
        {
            bool on = BoostEndUtcTicks > DateTime.UtcNow.Ticks;
            if (on == _boostApplied) return;
            _boostApplied = on;
            RecalculateStats();
        }

        // ================================================================== Deep Core mechanics

        // Drill rigs all work the frontier (the deepest layer) and stack: each digs like DrillPower miners without
        // taking a slot, and each adds DrillBore to how fast the layer breaks through, so every ore dug there (by
        // miners, taps or drills) counts that many times toward the breakthrough. Drills drive depth; auto-tap stays
        // a cash source. Overclock boosts both, on top of its ore value boost.
        public int DrillsActive { get { return (int)Math.Floor(Stats.Get(StatType.DrillCount) + 1e-6); } }
        public bool HasDrill(int layerIndex) { return DrillsActive > 0 && layerIndex == Layers.Count - 1; }
        double DrillOverclock { get { return _overclockApplied ? 1.0 + _overclockPowerAtStart : 1.0; } }
        double DrillWorkers(int layerIndex) { return HasDrill(layerIndex) ? Stats.Get(StatType.DrillPower) * DrillsActive * DrillOverclock : 0; }
        public double DrillBoreFactor(int layerIndex) { return HasDrill(layerIndex) ? 1.0 + Stats.Get(StatType.DrillBore) * DrillsActive * DrillOverclock : 1.0; }

        /// <summary>Cash per second the drills alone are producing on this layer.</summary>
        public double DrillIncome(MineLayer layer) { return DrillWorkers(layer.Index) * layer.OrePerMinerPerSecond * layer.ValuePerOre * Yield(layer); }

        /// <summary>Seconds until this layer breaks through from miners and drills (taps not counted), with or without
        /// the drills' help. Infinity when nothing is digging.</summary>
        public double BreakthroughSeconds(MineLayer layer, bool withDrills)
        {
            if (layer.Cleared) return 0;
            double workers = layer.Miners + (withDrills ? DrillWorkers(layer.Index) : 0);
            double rate = workers * layer.OrePerMinerPerSecond * _digSpeed * (withDrills ? DrillBoreFactor(layer.Index) : 1.0);
            return rate > 0 ? (layer.OreRequired - layer.Progress) / rate : double.PositiveInfinity;
        }

        // ---- dynamite
        public bool DynamiteUnlocked { get { return Stats.Get(StatType.DynamitePower) > 0; } }
        public float DynamiteChargeSeconds { get { return Config.dynamiteChargeSeconds / (float)Math.Max(0.1, Stats.Get(StatType.DynamiteCharge)); } }
        public float DynamiteCooldownSeconds { get { return Config.dynamiteCooldownSeconds * (float)(1.0 - Stats.Get(StatType.DynamiteCooldownCut)); } }
        public float DynamiteCooldownLeft { get { return Mathf.Max(0f, _dynamiteReadyAt - Time.unscaledTime); } }
        public bool DynamiteReady { get { return DynamiteUnlocked && DynamiteCooldownLeft <= 0f; } }

        /// <summary>Detonates on a layer. charge is 0..1 (how long the fuse was held). A full blast is worth
        /// DynamitePower seconds of income, spread over the layers it hits: the centre layer gets the biggest
        /// share and each layer further out (within DynamiteRadius) gets dynamiteFalloff times less. The ore
        /// still counts toward breakthroughs, so blasting the frontier digs deeper too.</summary>
        public BlastResult Blast(int layerIndex, float charge)
        {
            var result = new BlastResult { Center = layerIndex, Charge = charge };
            if (!DynamiteReady || layerIndex < 0 || layerIndex >= Layers.Count) return result;
            _dynamiteReadyAt = Time.unscaledTime + DynamiteCooldownSeconds;
            int radius = (int)Math.Floor(Stats.Get(StatType.DynamiteRadius) + 1e-6);
            double total = IncomeForMinutes(Stats.Get(StatType.DynamitePower) / 60.0) * Mathf.Clamp01(charge);
            int count = Layers.Count, lo = Math.Max(0, layerIndex - radius), hi = Math.Min(count - 1, layerIndex + radius);
            double weights = 0;
            for (int i = lo; i <= hi; i++) weights += Math.Pow(Config.dynamiteFalloff, Math.Abs(i - layerIndex));
            for (int i = lo; i <= hi; i++)
            {
                var layer = Layers[i];
                double share = total * Math.Pow(Config.dynamiteFalloff, Math.Abs(i - layerIndex)) / weights;
                double cash = AddOre(layer, share / Math.Max(1e-300, layer.ValuePerOre));
                result.Layers.Add(i);
                result.Money += cash;
            }
            result.Detonated = true;
            if (Blasted != null) Blasted(result);
            return result;
        }

        // ---- gems and power swings
        public bool GemsUnlocked { get { return Stats.Get(StatType.GemRate) > 0; } }
        public bool SwingsUnlocked { get { return Stats.Get(StatType.SwingRate) > 0; } }

        public double CollectGem()
        {
            double amount = IncomeForMinutes(Stats.Get(StatType.GemValue));
            GrantMoney(amount);
            return amount;
        }

        /// <summary>quality 1 = perfect, partial for a near miss, 0 = miss. A perfect swing strikes the frontier
        /// layer for SwingPower seconds of income (as ore, so it also digs).</summary>
        public double PowerSwing(double quality)
        {
            if (quality <= 0) return 0;
            var layer = Frontier;
            double cash = IncomeForMinutes(Stats.Get(StatType.SwingPower) / 60.0) * quality;
            return AddOre(layer, cash / Math.Max(1e-300, layer.ValuePerOre));
        }

        // ---- overclock
        public bool OverclockUnlocked { get { return Stats.Get(StatType.OverclockPower) > 0; } }
        public double OverclockMeter { get { return _overclockMeter; } }
        public bool OverclockActive { get { return _overclockApplied; } }
        public float OverclockSecondsLeft { get { return Mathf.Max(0f, _overclockEndsAt - Time.unscaledTime); } }

        public bool ActivateOverclock()
        {
            if (!OverclockUnlocked || OverclockActive || _overclockMeter < 1.0) return false;
            _overclockMeter = 0;
            _overclockEndsAt = Time.unscaledTime + (float)Stats.Get(StatType.OverclockDuration);
            _overclockPowerAtStart = Stats.Get(StatType.OverclockPower);
            _overclockApplied = true;
            RecalculateStats();
            if (OverclockStarted != null) OverclockStarted();
            return true;
        }

        void UpdateOverclock(float dt)
        {
            if (_overclockApplied)
            {
                if (Time.unscaledTime >= _overclockEndsAt) { _overclockApplied = false; RecalculateStats(); }
                return;
            }
            if (OverclockUnlocked)
                _overclockMeter = Math.Min(1.0, _overclockMeter + dt * Stats.Get(StatType.OverclockCharge) / Math.Max(1f, Config.overclockChargeSeconds));
        }

        // ================================================================== derived numbers

        void RecalculateStats()
        {
            ApplyBaseStats();
            Stats.ClearModifiers();
            foreach (var n in Tree.Nodes)
                if (n.Unlocked)
                    foreach (var e in n.Effects) Stats.Apply(e);
            foreach (var n in DeepTree.Nodes)
                if (n.Unlocked)
                    foreach (var e in n.Effects) Stats.Apply(e);
            foreach (var p in _ownedPerks)
                foreach (var e in p.Effects) Stats.Apply(e);

            // Paragon survives the tree reset that buying it causes, so it's reapplied here rather than
            // stored as a modifier that ClearModifiers() would wipe.
            double paragonMult = ParagonMultiplier;
            if (paragonMult != 1.0)
            {
                Stats.Apply(new SkillEffect(StatType.OreValue, ModOp.Multiply, paragonMult));
                Stats.Apply(new SkillEffect(StatType.MinerSpeed, ModOp.Multiply, paragonMult));
                Stats.Apply(new SkillEffect(StatType.TapPower, ModOp.Multiply, paragonMult));
                Stats.Apply(new SkillEffect(StatType.DigSpeed, ModOp.Multiply, paragonMult));
            }
            // Same for the ad boost and the Foreman Pass perk, which live outside the tree.
            if (_boostApplied) Stats.Apply(new SkillEffect(StatType.OreValue, ModOp.Multiply, Config.boostMultiplier));
            if (ForemanPass) Stats.Apply(new SkillEffect(StatType.OfflineEfficiency, ModOp.Percent, Config.foremanOfflineBonus));
            if (_overclockApplied) Stats.Apply(new SkillEffect(StatType.OreValue, ModOp.Multiply, 1.0 + _overclockPowerAtStart));

            _digSpeed = Stats.Get(StatType.DigSpeed);
            _critChance = Stats.Get(StatType.CritChance);
            _critMult = Stats.Get(StatType.CritMultiplier);
            _autoTapRate = Stats.Get(StatType.AutoTapRate);

            foreach (var l in Layers) RecalculateLayer(l);
            EnforceMinerLimits();
            RecalculateIncome();
        }

        void RecalculateLayer(MineLayer l)
        {
            int i = l.Index;
            double hardness = Math.Pow(Config.hardnessGrowth, i);
            l.Richness = Richness(i);
            l.OreRequired = Config.baseOreRequired * Math.Pow(Config.oreRequiredGrowth, i);
            l.ValuePerOre = Config.baseOreValue * Math.Pow(Config.oreValueGrowth, i) * l.Richness
                            * Stats.Get(StatType.OreValue) * (1.0 + Stats.Get(StatType.DepthBonus) * i);
            l.OrePerMinerPerSecond = Config.baseMinerOrePerSecond * Stats.Get(StatType.MinerSpeed) / hardness;
            l.OrePerTap = Config.baseTapOre * Stats.Get(StatType.TapPower) / hardness;
        }

        /// <summary>Per-layer value roll: 0.7x to 1.5x, with a x3 Motherlode every N layers.
        /// This is what makes distributing miners a real choice instead of "always the deepest".</summary>
        public double Richness(int i)
        {
            if (i == 0) return 1.0;
            if (Config.motherlodeEvery > 0 && i % Config.motherlodeEvery == Config.motherlodeEvery - 1) return 3.0;
            return 0.7 + 0.8 * Hash.Hash01(Config.layerSeed, i, 7, 11);
        }

        /// <summary>Run earnings needed to go from a Paragon level to the next one (mirrors how skill node
        /// costs grow).</summary>
        public double ParagonLevelCost(int level)
        {
            return Config.paragonBaseRequirement * Math.Pow(Config.paragonRequirementGrowth, Math.Max(0, level));
        }

        void RecalculateIncome()
        {
            double inc = 0;
            foreach (var l in Layers)
                inc += LayerIncome(l);
            IncomePerSecond = inc;
        }

        void EnforceMinerLimits()
        {
            int slots = SlotsPerLayer, assigned = 0;
            foreach (var l in Layers)
            {
                l.Miners = Mathf.Clamp(l.Miners, 0, slots);
                assigned += l.Miners;
            }
            // More assigned than owned (e.g. tree changed between versions): trim from the top.
            for (int i = 0; i < Layers.Count && assigned > TotalMiners; i++)
            {
                int cut = Math.Min(Layers[i].Miners, assigned - TotalMiners);
                Layers[i].Miners -= cut;
                assigned -= cut;
            }
            AssignedMiners = assigned;
        }

        // ================================================================== save / load / offline

        public void Save()
        {
            if (!_initialized) return;
            var d = new SaveData
            {
                money = Money,
                lifetimeOre = LifetimeOre,
                lifetimeMoney = LifetimeMoney,
                totalTaps = TotalTaps,
                paragonLevel = ParagonLevel,
                runMoney = _runMoney,
                deepCoreUnlocked = DeepCoreUnlocked,
                boostEndUtcTicks = BoostEndUtcTicks,
                lastSaveUtcTicks = DateTime.UtcNow.Ticks,
            };
            foreach (var l in Layers) d.layers.Add(new LayerSave { progress = l.Progress, miners = l.Miners });
            foreach (var n in Tree.Nodes) if (n.Unlocked) d.unlockedNodes.Add(n.Id);
            foreach (var n in DeepTree.Nodes) if (n.Unlocked) d.unlockedNodes.Add(n.Id);
            foreach (var p in _ownedPerks) d.paragonPerks.Add(p.Id);
            SaveSystem.Save(d);
        }

        void ApplySave(SaveData s)
        {
            Money = s.money;
            LifetimeOre = s.lifetimeOre;
            LifetimeMoney = s.lifetimeMoney;
            TotalTaps = s.totalTaps;
            ParagonLevel = s.paragonLevel;
            BoostEndUtcTicks = s.boostEndUtcTicks;
            _runMoney = s.runMoney;
            if (s.version < 2)
            {
                // v1 measured Paragon progress on lifetime earnings: carry over what was past the current level.
                double g = Config.paragonRequirementGrowth, banked = 0;
                for (int i = 0; i < ParagonLevel; i++) banked += Config.paragonBaseRequirement * Math.Pow(g, i);
                _runMoney = Math.Max(0, LifetimeMoney - banked);
            }

            _ownedPerks.Clear();
            ParagonPointsSpent = 0;
            // Perk ids from before the endless tree (L0, R3, cap...) no longer exist: those points come back.
            int deepest = 0;
            foreach (var id in s.paragonPerks) deepest = Math.Max(deepest, ParagonTree.TierOf(id));
            ParagonTree.EnsureTiers(Math.Min(deepest, 100000));
            foreach (var id in s.paragonPerks)
            {
                var perk = ParagonTree.Get(id);
                if (perk != null && _ownedPerks.Add(perk)) ParagonPointsSpent += perk.Cost;
            }

            DeepCoreUnlocked = s.deepCoreUnlocked;
            Tree.ResetOwnership();
            DeepTree.ResetOwnership();
            foreach (var id in s.unlockedNodes)
            {
                var n = Tree.Get(id) ?? DeepTree.Get(id);
                if (n != null) n.Owner.Unlock(n);
            }

            Layers.Clear();
            int count = Math.Max(1, s.layers.Count);
            for (int i = 0; i < count; i++)
            {
                var l = new MineLayer(i);
                if (i < s.layers.Count) { l.Progress = s.layers[i].progress; l.Miners = s.layers[i].miners; }
                Layers.Add(l);
            }

            RecalculateStats();
            PlaceFreeMiners();
        }

        void SimulateOffline(long lastSaveTicks)
        {
            if (lastSaveTicks <= 0) return;
            double away = (DateTime.UtcNow.Ticks - lastSaveTicks) / (double)TimeSpan.TicksPerSecond;
            if (away < Config.minOfflineSeconds) return; // also rejects clocks set backwards

            double capped = Math.Min(away, Stats.Get(StatType.OfflineCapHours) * 3600.0);
            double effective = capped * Stats.Get(StatType.OfflineEfficiency);
            if (effective <= 0) return;

            double moneyBefore = Money;
            int depthBefore = Depth;

            // The boost is wall-clock, so only the part of the absence it covered gets it: those steps run first.
            const int steps = 120;
            double boostedSeconds = Math.Min(capped, Math.Max(0, (BoostEndUtcTicks - lastSaveTicks) / (double)TimeSpan.TicksPerSecond));
            int boostedSteps = (int)Math.Round(steps * boostedSeconds / capped);

            // Chunked so breakthroughs part-way through let free miners move deeper.
            for (int i = 0; i < steps; i++)
            {
                bool boosted = i < boostedSteps;
                if (boosted != _boostApplied) { _boostApplied = boosted; RecalculateStats(); }
                Tick(effective / steps, false);
            }
            SyncBoost();
            foreach (var l in Layers) l.PendingIncome = 0;

            var report = new OfflineReport
            {
                AwaySeconds = away,
                SimulatedSeconds = capped,
                Money = Money - moneyBefore,
                LayersGained = Depth - depthBefore,
            };
            var prev = PendingOfflineReport; // not collected yet: merge
            if (prev != null)
            {
                report.AwaySeconds += prev.AwaySeconds;
                report.SimulatedSeconds += prev.SimulatedSeconds;
                report.Money += prev.Money;
                report.LayersGained += prev.LayersGained;
            }
            PendingOfflineReport = report;
        }

        // Mobile apps are usually suspended rather than quit, so offline progress is also granted on resume.
        void OnApplicationPause(bool paused)
        {
            if (!_initialized) return;
            if (paused)
            {
                Save();
                _pausedAtTicks = DateTime.UtcNow.Ticks;
            }
            else if (_pausedAtTicks > 0)
            {
                if (!SuppressOfflineProgress) SimulateOffline(_pausedAtTicks);
                _pausedAtTicks = 0;
            }
        }

        void OnApplicationQuit() { Save(); }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || IDLEMINE_DEBUG
        // ================================================================== debug tools (used by DebugMenu)

        public void DebugGrantMoney(double amount) { GrantMoney(amount); }

        /// <summary>Owns every node in a tree for free. Completing the skill tree unlocks the Deep Core as normal.</summary>
        public void DebugUnlockTree(SkillTree tree)
        {
            if (tree == DeepTree) DeepCoreUnlocked = true;
            foreach (var n in tree.Nodes) tree.Unlock(n);
            RecalculateStats();
            PlaceFreeMiners();
            if (tree == Tree && !DeepCoreUnlocked)
            {
                DeepCoreUnlocked = true;
                if (DeepCoreOpened != null) DeepCoreOpened();
            }
            Save();
        }

        public void DebugUnlockDeepCore() { DeepCoreUnlocked = true; Save(); }

        /// <summary>Adds Paragon levels without resetting the run.</summary>
        public void DebugAddParagonLevels(int levels)
        {
            ParagonLevel = Math.Max(0, ParagonLevel + levels);
            GrowParagonTree();
            RecalculateStats();
            Save();
        }

        public void DebugFillParagonProgress() { _runMoney = Math.Max(_runMoney, NextParagonCost); }

        /// <summary>Pretends the player was away for this long (shows the Welcome back popup).</summary>
        public void DebugSimulateOffline(double seconds)
        {
            SimulateOffline(DateTime.UtcNow.Ticks - (long)(seconds * TimeSpan.TicksPerSecond));
        }

        public void DebugFillOverclock() { _overclockMeter = 1.0; }
        public void DebugResetDynamite() { _dynamiteReadyAt = 0f; }

        /// <summary>Deletes the save (and ad/purchase state) and reloads the scene for a brand new game.</summary>
        public void DebugWipeAndRestart()
        {
            _initialized = false; // stop any save from rewriting it
            SaveSystem.Delete();
            MonetizationStore.Delete();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
#endif

        // ================================================================== debug helpers (right-click the component)

        [ContextMenu("Debug/Add 1000x current money")]
        void DebugAddMoney() { Money = Math.Max(1000, Money * 1000); }

        [ContextMenu("Debug/Grant 1 Paragon level")]
        void DebugGrantParagon() { _runMoney = Math.Max(_runMoney, NextParagonCost); }

        [ContextMenu("Debug/Wipe save and restart")]
        void DebugWipe()
        {
            SaveSystem.Delete();
            MonetizationStore.Delete();
            _initialized = false; // stop the quit-save from rewriting it
            Debug.Log("[IdleMine] Save (and ad/purchase state) wiped. Stop and re-enter Play mode.");
        }
    }
}
