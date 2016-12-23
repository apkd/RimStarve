using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;

namespace Verse
{
    /// <summary>
    /// Extends <see cref="Pawn"/> to provide additional graphic sets that are activated
    /// based on a set of conditions.
    /// </summary>
    public class RimStarvePawn : Pawn
    {
        /// <summary> List of graphic replacements </summary>
        private List<GraphicReplacement> replacements = new List<GraphicReplacement>();

        /// <summary> Tracks the current life stage to see when it changed </summary>
        private PawnKindLifeStage lifeStageBefore;

        /// <summary> Cache ModContentHolder for this mod </summary>
        private ModContentHolder<Texture2D> contentHolder = LoadedModManager.RunningMods
            .FirstOrDefault(x => x.Name == "RimStarve")
            .GetContentHolder<Texture2D>();

        private CompShearable compShearable;

        private bool IsSheared => compShearable?.Fullness < 0.1;
        private bool IsFighting => CurJob?.def == JobDefOf.AttackMelee;
        private bool IsMoving => pather.MovingNow;
        private bool IsTamed => Faction == Faction.OfPlayer;
        private bool IsLyingDown => Downed || (jobs?.curDriver?.asleep ?? true) || !health.capacities.CanBeAwake;
        private bool IsCold => GenTemperature.GetTemperatureAtCellOrCaravanTile(this) <= 0;
        private bool IsWinter => GenDate.Season(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(Find.VisibleMap.Tile).x) == Season.Winter;
        private bool IsVolcanicWinter => Map.mapConditionManager.ConditionIsActive(MapConditionDefOf.VolcanicWinter);

        private void SetupReplacements()
        {
            // cache the CompShearable to avoid expensive calls every tick
            compShearable = compShearable ?? GetComp<CompShearable>();

            // load replacements for existing graphics based on the current life stage
            replacements.Clear();

            AddReplacement(
                graphic: LoadGraphic("winter_sleep"),
                condition: () => IsCold && IsLyingDown);

            AddReplacement(
                graphic: LoadGraphic("winter"),
                condition: () => IsCold);

            AddReplacement(
                graphic: LoadGraphic("sheared_sleep"),
                condition: () => IsTamed && IsSheared && IsLyingDown);

            AddReplacement(
                graphic: LoadGraphic("sheared"),
                condition: () => IsTamed && IsSheared);

            AddReplacement(
                graphic: LoadGraphic("sleep"),
                condition: () => IsLyingDown);

            AddReplacement( // default graphic if no previous match
                graphic: ageTracker.CurKindLifeStage.bodyGraphicData.Graphic,
                condition: () => true);
        }

        /// <summary> Add a graphic replacement rule </summary> 
        private void AddReplacement(Graphic graphic, System.Func<bool> condition)
        {
            if (graphic == null) return; // skip if graphic could not be loaded
            replacements.Add(new GraphicReplacement(graphic, condition));
        }

        /// <summary> Try to load the Graphic of appropriate type based on current life stage </summary> 
        private Graphic LoadGraphic(string variant)
        {
            string path = ageTracker.CurKindLifeStage.bodyGraphicData.texPath + "_" + variant;

            // determine graphic class type
            System.Type type = null;
            if (contentHolder.Get(path + "_front") != null)
                type = typeof(Graphic_Multi);
            else if (contentHolder.Get(path) != null)
                type = typeof(Graphic_Single);
            if (type == null) return null; // texture not found, skip graphic

            var data = new GraphicData();
            data.CopyFrom(ageTracker.CurKindLifeStage.bodyGraphicData);
            data.texPath = path;
            data.graphicClass = type;
            return data.Graphic;
        }

        /// <summary> Updates the active graphic based on a set of rules </summary>
        public void UpdateActiveGraphic()
        {

            // attempt to find a replacement graphic
            foreach (var replacement in replacements)
            {
                if (replacement.TryReplace(Drawer.renderer))
                    break; // break on first successful replacement
            }
        }

        /// <summary> Executed every time the pawn is updated </summary>
        public override void Tick()
        {
            base.Tick();

            if (pather == null) return; // this can occur if the pawn leaves the map area

            // initialize the replacements (once per lifestage)
            if (lifeStageBefore != ageTracker.CurKindLifeStage)
            {
                SetupReplacements();
                lifeStageBefore = ageTracker.CurKindLifeStage;
            }

            UpdateActiveGraphic();
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);

            // try to run from attacker
            if (!Dead && !Downed && def.race.manhunterOnDamageChance == 0)
            {
                IntVec3 wander_dest = RCellFinder.RandomWanderDestFor(this, Position, 12f, null, Danger.Deadly);
                if (wander_dest.IsValid)
                {
                    jobs.StopAll();
                    jobs.StartJob(new AI.Job(JobDefOf.GotoWander, targetA: wander_dest)
                    {
                        expiryInterval = 1500,
                        locomotionUrgency = AI.LocomotionUrgency.Sprint
                    });
                }
            }

            // update graphic if died
            if (Dead)
            {
                UpdateActiveGraphic();
            }
        }
    }
}
