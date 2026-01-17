using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace BathTime;

/// <summary>
/// Fixes a null reference issue in the mashtubs mod when stomping on a container with no mashable in the MashSlot.
/// </summary>
[HarmonyPatch("Mash_Tubs.src.Blocks.BlockEntityMashTub", "OnEntityStomp")]
public static class MashTubsPatch
{
    public static bool Prefix(object __instance, ICoreAPI api, Entity entity)
    {
        if (__instance.GetType()?.GetProperty("mashStack")?.GetValue(__instance) is null)
        {
            return false;
        }
        return true;
    }
}
