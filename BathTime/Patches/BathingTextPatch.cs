using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using static Vintagestory.API.Client.GuiDialog;

namespace BathTime;


[HarmonyPatch(typeof(CharacterExtraDialogs))]
public static class BathingTextPatch
{
    [HarmonyPatch("ComposeStatsGui")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> AddExtraUICall(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);

        // Add call to insert text.
        codeMatcher.MatchStartForward(
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(GuiComposer), "EndChildElements"))
        );

        codeMatcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(CharacterExtraDialogs), "capi")),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(CharacterExtraDialogs), "Composers")),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BathingTextPatch), nameof(ComposeBathingText)))
        );

        return codeMatcher.InstructionEnumeration();
    }


    private static void ComposeBathingText(CharacterExtraDialogs __instance, ICoreClientAPI capi, DlgComposers composers)
    {
        if (composers is null) return;
        if (!composers.ContainsKey("playerstats")) return;
        if (!composers.ContainsKey("environment")) return;

        if (EntityBehaviorStinky.IsBathing(capi.World.Player.Entity))
        {
            ElementBounds bounds = ElementBounds.Fixed(0.0, composers["playerstats"].LastAddedElementBounds.BelowCopy().fixedY, 140.0, 20.0);

            composers["playerstats"].AddRichtext(
                Lang.Get("bathtime:bath-indicator"), CairoFont.WhiteDetailText(), bounds
            );
        }
    }



}