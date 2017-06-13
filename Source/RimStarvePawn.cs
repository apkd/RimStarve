using System.Collections.Generic;
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
		List<GraphicReplacement> replacements = new List<GraphicReplacement>();

		/// <summary> Tracks the current life stage to see when it changed </summary>
		PawnKindLifeStage lifeStageBefore;

		/// <summary> Cache ModContentHolder for this mod </summary>
		ModContentHolder<Texture2D> contentHolder = LoadedModManager.RunningMods
			.FirstOrDefault(x => x.Name == "RimStarve")
			.GetContentHolder<Texture2D>();

		CompHasGatherableBodyResource compShearable;
		CompHasGatherableBodyResource compElectricity;

		bool IsSheared => compShearable?.Fullness < 0.1f;
		bool IsEnergized => compElectricity?.Fullness > 0.9f;
		bool IsFighting => CurJob?.def == JobDefOf.AttackMelee;
		bool IsMoving => pather.MovingNow;
		bool IsTamed => Faction == Faction.OfPlayer;
		bool IsLyingDown => Downed || (jobs?.curDriver?.asleep ?? true) || !health.capacities.CanBeAwake;
		bool IsCold => GetTemperature() <= 0;
		bool IsHot => GetTemperature() >= 42;

		float lastTemperature = 20;
		float GetTemperature()
		{
			if (!Dead) // avoid null reference exception
				GenTemperature.TryGetAirTemperatureAroundThing(this, out lastTemperature);

			return lastTemperature;
		}

		void SetupReplacements()
		{
			// cache the comps to avoid expensive calls every tick
			compShearable = compShearable ?? GetComp<CompShearable>();
			compElectricity = compElectricity ?? GetComp<CompStaticElectricity>();

			// load replacements for existing graphics based on the current life stage
			replacements.Clear();

			AddReplacement(
				graphic: LoadGraphic("summer_dead"),
				condition: () => Dead && IsHot);

			AddReplacement(
				graphic: LoadGraphic("winter_dead"),
				condition: () => Dead && IsCold);

			AddReplacement(
				graphic: LoadGraphic("dead"),
				condition: () => Dead);

			AddReplacement(
				graphic: LoadGraphic("summer_sleep"),
				condition: () => IsHot && IsLyingDown);

			AddReplacement(
				graphic: LoadGraphic("winter_sleep"),
				condition: () => IsCold && IsLyingDown);

			AddReplacement(
				graphic: LoadGraphic("summer"),
				condition: () => IsHot);

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
				graphic: LoadGraphic("charged"),
				condition: () => IsEnergized);

			AddReplacement(
				graphic: LoadGraphic("sleep"),
				condition: () => IsLyingDown);

			AddReplacement( // default graphic if no previous match
				graphic: ageTracker.CurKindLifeStage.bodyGraphicData.Graphic,
				condition: () => true);
		}

		/// <summary> Add a graphic replacement rule </summary> 
		void AddReplacement(Graphic graphic, System.Func<bool> condition)
		{
			if (graphic == null) return; // skip if graphic could not be loaded
			replacements.Add(new GraphicReplacement(graphic, condition));
		}

		/// <summary> Try to load the Graphic of appropriate type based on current life stage </summary> 
		Graphic LoadGraphic(string variant)
		{
			string path = ageTracker.CurKindLifeStage.bodyGraphicData.texPath + "_" + variant;

			// determine graphic class type
			System.Type type = null;
			if (contentHolder.Get(path + "_front") != null)
				type = typeof(Graphic_Multi);
			else if (contentHolder.Get(path) != null)
				type = typeof(Graphic_Single);

			if (type == null)
				return null; // texture not found, skip graphic

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
		public override void TickRare()
		{
			base.TickRare();

			if (pather == null) return; // this can occur if the pawn leaves the map area
			if (ageTracker == null) return;

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
