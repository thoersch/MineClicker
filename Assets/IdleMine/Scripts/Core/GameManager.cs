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

        /// <summary>How many Paragon levels lifetime earnings have unlocked beyond the one already owned.</summary>
        public int AvailableParagonLevels { get { return Math.Max(0, ParagonLevelForLifetime(LifetimeMoney) - ParagonLevel); } }

        public bool CanAscend { get { return AvailableParagonLevels > 0; } }

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

        double _digSpeed, _critChance, _critMult, _autoTapRate;
        double _autoTapAccum;
        float _saveTimer;
        bool _initialized;
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

            var save = SaveSystem.Load();
            if (save != null) ApplySave(save);
            else NewGame();

            _initialized = true;
            if (save != null) SimulateOffline(save.lastSaveUtcTicks);
        }

        /// <summary>Resets money, miners and the skill tree back to a fresh start. Used both for a brand
        /// new save and for Ascend, which calls this after banking a new Paragon level.</summary>
        void NewGame()
        {
            Money = Config.startingMoney;
            AssignedMiners = 0;
            Tree.ResetOwnership();
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
        }

        // ================================================================== frame loop

        void Update()
        {
            if (!_initialized) return;
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
                if (layer.Miners <= 0) continue;
                double ore = layer.Miners * layer.OrePerMinerPerSecond * Yield(layer) * dt;
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
            return layer.Miners * layer.OrePerMinerPerSecond * layer.ValuePerOre * Yield(layer);
        }

        /// <summary>Sells ore, advances the breakthrough bar. Returns the cash earned.</summary>
        double AddOre(MineLayer layer, double ore)
        {
            if (ore <= 0) return 0;
            double cash = ore * layer.ValuePerOre;
            Money += cash;
            LifetimeMoney += cash;
            LifetimeOre += ore;

            if (!layer.Cleared)
            {
                layer.Progress += ore * _digSpeed;
                if (layer.Progress >= layer.OreRequired)
                {
                    layer.Progress = layer.OreRequired;
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

        public bool CanPurchase(SkillNode n)
        {
            return n != null && Tree.IsAvailable(n) && CanAfford(n);
        }

        public bool TryPurchase(SkillNode n)
        {
            if (!CanPurchase(n)) return false;
            Money -= GetCost(n);
            Tree.Unlock(n);

            int minersBefore = TotalMiners, slotsBefore = SlotsPerLayer;
            RecalculateStats();
            if (TotalMiners != minersBefore || SlotsPerLayer != slotsBefore) PlaceFreeMiners();

            if (NodePurchased != null) NodePurchased(n);
            return true;
        }

        /// <summary>Cashes in every Paragon level lifetime earnings have unlocked: banks the level(s),
        /// then wipes money, miners and the skill tree back to a fresh start. LifetimeOre/LifetimeMoney
        /// and TotalTaps are career totals and are never reset, so Paragon progress never goes backwards.</summary>
        public bool Ascend()
        {
            int gain = AvailableParagonLevels;
            if (gain <= 0) return false;
            ParagonLevel += gain;
            NewGame();
            if (Ascended != null) Ascended(gain);
            Save();
            return true;
        }

        // ================================================================== derived numbers

        void RecalculateStats()
        {
            ApplyBaseStats();
            Stats.ClearModifiers();
            foreach (var n in Tree.Nodes)
                if (n.Unlocked)
                    foreach (var e in n.Effects) Stats.Apply(e);

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

        /// <summary>Total lifetime earnings needed to reach a given Paragon level (geometric sum of the
        /// per-level cost, mirroring how skill node costs grow).</summary>
        public double ParagonRequirement(int level)
        {
            if (level <= 0) return 0;
            double req0 = Config.paragonBaseRequirement;
            double growth = Config.paragonRequirementGrowth;
            if (Math.Abs(growth - 1.0) < 1e-9) return req0 * level;
            return req0 * (Math.Pow(growth, level) - 1.0) / (growth - 1.0);
        }

        /// <summary>Highest Paragon level a given lifetime-earnings total can afford.</summary>
        public int ParagonLevelForLifetime(double lifetimeMoney)
        {
            double req0 = Config.paragonBaseRequirement;
            if (lifetimeMoney < req0) return 0;
            double growth = Config.paragonRequirementGrowth;
            int level = Math.Abs(growth - 1.0) < 1e-9
                ? (int)Math.Floor(lifetimeMoney / req0)
                : (int)Math.Floor(Math.Log(lifetimeMoney * (growth - 1.0) / req0 + 1.0, growth) + 1e-9);

            // Closed-form log can be off by one near the boundary from floating point error; settle exactly.
            while (level > 0 && ParagonRequirement(level) > lifetimeMoney) level--;
            while (ParagonRequirement(level + 1) <= lifetimeMoney) level++;
            return Math.Max(0, level);
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
                lastSaveUtcTicks = DateTime.UtcNow.Ticks,
            };
            foreach (var l in Layers) d.layers.Add(new LayerSave { progress = l.Progress, miners = l.Miners });
            foreach (var n in Tree.Nodes) if (n.Unlocked) d.unlockedNodes.Add(n.Id);
            SaveSystem.Save(d);
        }

        void ApplySave(SaveData s)
        {
            Money = s.money;
            LifetimeOre = s.lifetimeOre;
            LifetimeMoney = s.lifetimeMoney;
            TotalTaps = s.totalTaps;
            ParagonLevel = s.paragonLevel;

            Tree.ResetOwnership();
            foreach (var id in s.unlockedNodes)
            {
                var n = Tree.Get(id);
                if (n != null) Tree.Unlock(n);
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

            // Chunked so breakthroughs part-way through let free miners move deeper.
            const int steps = 120;
            for (int i = 0; i < steps; i++) Tick(effective / steps, false);
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
                SimulateOffline(_pausedAtTicks);
                _pausedAtTicks = 0;
            }
        }

        void OnApplicationQuit() { Save(); }

        // ================================================================== debug helpers (right-click the component)

        [ContextMenu("Debug/Add 1000x current money")]
        void DebugAddMoney() { Money = Math.Max(1000, Money * 1000); }

        [ContextMenu("Debug/Grant 1 Paragon level")]
        void DebugGrantParagon() { LifetimeMoney = Math.Max(LifetimeMoney, ParagonRequirement(ParagonLevel + 1)); }

        [ContextMenu("Debug/Wipe save and restart")]
        void DebugWipe()
        {
            SaveSystem.Delete();
            _initialized = false; // stop the quit-save from rewriting it
            Debug.Log("[IdleMine] Save wiped. Stop and re-enter Play mode.");
        }
    }
}
