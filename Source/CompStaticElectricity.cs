using System.Linq;
using Verse;
using Verse.Sound;

namespace RimWorld
{
    public class CompProperties_CompStaticElectricity : CompProperties
    {
        public int powerPerDay = 200;
        public int maxChargeDays = 2;

        public CompProperties_CompStaticElectricity()
        {
            compClass = typeof(CompStaticElectricity);
        }
    }

    public class CompStaticElectricity : CompHasGatherableBodyResource
    {
        protected override int GatherResourcesIntervalDays => Props.maxChargeDays;
        protected override int ResourceAmount => Props.powerPerDay;
        protected override ThingDef ResourceDef => null;
        protected override string SaveKey => "powerHarvestableGrowth";
        protected override bool Active => (parent as Pawn)?.Awake() ?? false;
        public CompProperties_CompStaticElectricity Props => props as CompProperties_CompStaticElectricity;

        private WeatherDef[] weathers = {
            WeatherDef.Named("Rain"), WeatherDef.Named("FoggyRain"),
            WeatherDef.Named("RainyThunderstorm"), WeatherDef.Named("DryThunderstorm"),
        };

        private const int frameskip = 150;
        private int frameskip_offset = Rand.Range(0, frameskip);

        public override void CompTick()
        {
            base.CompTick();

            if (parent.DestroyedOrNull()) return;
            if ((GenTicks.TicksAbs + frameskip_offset) % frameskip != 0) return;
            if (fullness == 0) return;

            var battery = GenClosest.ClosestThing_Global(
                center: parent.Position,
                searchSet: parent.Map.listerThings.ThingsMatching(ThingRequest.ForDef(ThingDefOf.Battery)),
                maxDistance: 2f + 5f * fullness,
                validator: t => !t.Destroyed);

            if (battery != null)
            {
                battery.TryGetComp<CompPowerBattery>()?.AddEnergy(fullness * ResourceAmount * GatherResourcesIntervalDays);

                SoundDef.Named("PowerOnSmall").PlayOneShot(new TargetInfo(parent.Position, parent.Map, false));
                var loc = parent.Position.ToVector3Shifted();

                MoteMaker.ThrowLightningGlow(loc, parent.Map, 0.5f + fullness);
                if (fullness > 0.3) MoteMaker.ThrowMicroSparks(loc, parent.Map);

                fullness = 0; (parent as RimStarvePawn)?.UpdateActiveGraphic();
            }

            if (Fullness == 1f && Rand.Chance(0.01f) && weathers.Contains(parent.Map.weatherManager.curWeather))
            {
                new WeatherEvent_LightningStrike(parent.Map, parent.Position).FireEvent();
                fullness = 0; (parent as RimStarvePawn)?.UpdateActiveGraphic();
            }
        }

        private string ElectricityString()
        {
            if (!Active) return null;
            if (fullness > 0.9) return "Full charge";
            if (fullness > 0.75) return "High charge";
            if (fullness > 0.3) return "Medium charge";
            return "Low charge";
        }

        public override string CompInspectStringExtra()
        {
            if (!Active) return null;
            return "Static electricity: " + ElectricityString();
        }
    }
}
