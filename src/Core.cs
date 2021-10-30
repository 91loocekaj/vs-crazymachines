using HarmonyLib;
using qptech.src;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CrazyMachines
{
    public class Core : ModSystem
    {
        private Harmony harmony;
        ICoreAPI api;

        public override void StartPre(ICoreAPI api)
        {
            try
            {
                CMConfig FromDisk;
                if ((FromDisk = api.LoadModConfig<CMConfig>("CrazyMachinesConfig.json")) == null)
                {
                    api.StoreModConfig<CMConfig>(CMConfig.Loaded, "CrazyMachinesConfig.json");
                }
                else CMConfig.Loaded = FromDisk;
            }
            catch
            {
                api.StoreModConfig<CMConfig>(CMConfig.Loaded, "CrazyMachinesConfig.json");
            }

            base.StartPre(api);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            api.RegisterBlockEntityClass("BEStabilizer", typeof(BEStabilizer));
            api.RegisterBlockEntityClass("BERadiator", typeof(BERadiator));

            harmony = new Harmony("com.jakecool19.crazymachines.patches");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            api.Event.OnTrySpawnEntity += Event_OnTrySpawnEntity;
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }

        private bool Event_OnTrySpawnEntity(ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            bool allow = true;

            if (!properties.Code.Path.StartsWithFast("drifter")) return allow;

            api.ModLoader.GetModSystem<StabilizerRegistry>()?.GetNearestPoi(spawnPosition, CMConfig.Loaded.MaxStabilizerRadius, (poi) =>
            {
                if ((poi as IStabilizePOI)?.Stabilize() == true)
                {
                    allow = false;
                    return true;
                }

                return false;
            });

            return allow;
        }

        }

    public class CMConfig
    {
        public static CMConfig Loaded { get; set; } = new CMConfig();

        public int MaxNetworkLength { get; set; } = 30;

        public int MaxStabilizerRadius { get; set; } = 30;
    }

    public class BERadiator : BEEBaseDevice, IHeatSource
    {
        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return (Capacitor > 0 && IsOn)? 10 : 0;
        }
    }
}
