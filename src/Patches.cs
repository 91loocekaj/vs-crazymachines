using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CrazyMachines
{
    [HarmonyPatch(typeof(SystemTemporalStability))]
    public class TemporalPatches
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            //From Melchoir
            if (original != null)
            {
                foreach (var patched in harmony.GetPatchedMethods())
                {
                    if (patched.Name == original.Name) return false;
                }
            }

            return true;
        }

        [HarmonyPatch("Event_OnTrySpawnEntity")]
        [HarmonyPrefix]
        static bool StormTrySpawnOverride(Vec3d spawnPosition, ICoreAPI ___api, ref bool __result)
        {
            bool allow = true;

            ___api.ModLoader.GetModSystem<StabilizerRegistry>()?.GetNearestPoi(spawnPosition, CMConfig.Loaded.MaxStabilizerRadius, (poi) =>
            {
                if ((poi as IStabilizePOI)?.Stabilize() == true)
                {
                    allow = false;
                    return true;
                }

                return false;
            });
            
            __result = allow;
            return allow;
        }

        [HarmonyPatch("DoSpawn")]
        [HarmonyPrefix]
        static bool StormSpawnOverride(Vec3d spawnPosition, ICoreAPI ___api)
        {
            bool allow = true;

            ___api.ModLoader.GetModSystem<StabilizerRegistry>()?.GetNearestPoi(spawnPosition, CMConfig.Loaded.MaxStabilizerRadius, (poi) =>
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

        [HarmonyPatch("GetTemporalStability", new Type[] { typeof(double), typeof(double), typeof(double) })]
        [HarmonyPrefix]
        static bool StabilityOverride(double x, double y, double z, ICoreAPI ___api, ref float __result)
        {
            bool allow = true;
            Vec3d loc = new Vec3d(x,y,z);

            ___api.ModLoader.GetModSystem<StabilizerRegistry>()?.GetNearestPoi(loc, CMConfig.Loaded.MaxStabilizerRadius, (poi) =>
            {
                if ((poi as IStabilizePOI)?.Stabilize() == true)
                {
                    allow = false;
                    return true;
                }

                return false;
            });
            
            __result = !allow ? 1.5f : 0f;

            return allow;
        }
    }
}
