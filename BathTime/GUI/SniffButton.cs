using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static Vintagestory.API.Client.GuiDialog;

namespace BathTime;


[HarmonyPatch(typeof(CharacterExtraDialogs))]
public static class CharacterExtraDialogsPatch
{
    [HarmonyPatch("ComposeStatsGui")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> AddExtraUICall(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);

        // Find the dialog bounds operand to pass to load for ComposeSniffButton
        codeMatcher.MatchEndForward(
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ElementBounds), "get_InnerHeight"))
        ).MatchEndForward(
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ElementBounds), "WithFixedAlignmentOffset"))
        ).Advance(1);

        object dialogBoundsOperand = codeMatcher.Instruction.operand;
        if (dialogBoundsOperand is null)
        {
            OpCode opCode = codeMatcher.Instruction.opcode;
            if (opCode == OpCodes.Stloc_0) dialogBoundsOperand = 0;
            if (opCode == OpCodes.Stloc_1) dialogBoundsOperand = 1;
            if (opCode == OpCodes.Stloc_2) dialogBoundsOperand = 2;
            if (opCode == OpCodes.Stloc_3) dialogBoundsOperand = 3;
        }

        // Add call to insert button.
        codeMatcher.MatchStartForward(
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(GuiComposer), "Compose", [typeof(bool)]))
        );

        codeMatcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(CharacterExtraDialogs), "capi")),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(CharacterExtraDialogs), "Composers")),
            new CodeInstruction(OpCodes.Ldloc_S, dialogBoundsOperand),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CharacterExtraDialogsPatch), nameof(ComposeSniffButton)))
        );

        return codeMatcher.InstructionEnumeration();
    }

    private static void ComposeSniffButton(CharacterExtraDialogs __instance, ICoreClientAPI capi, DlgComposers composers, ElementBounds dialogBounds)
    {
        if (composers is null) return;
        if (!composers.ContainsKey("playerstats")) return;
        if (!composers.ContainsKey("environment")) return;
        if (dialogBounds is null) return;

        ElementBounds bounds = new ElementBounds().WithAlignment(EnumDialogArea.LeftBottom).WithFixedAlignmentOffset(0, 40).WithFixedSize(180, 40);

        composers["playerstats"].AddSmallButton(
            Lang.Get("bathtime:sniff"),
            () =>
            {
                PlaySniffAnimation(capi);
                PrintSniffAlert(capi);
                return true;
            },
            bounds,
            key: "sniff_button"
        );
    }

    private static void PlaySniffAnimation(ICoreClientAPI capi)
    {
        EntityPlayer entityPlayer = capi.World.Player.Entity;

        if (capi.World.Player.CameraMode == EnumCameraMode.FirstPerson)
        {
            entityPlayer.AnimManager.StartAnimation("headscratch-fp");
            entityPlayer.AnimManager.StartAnimation("cough-fp");
        }
        else
        {
            entityPlayer.AnimManager.StartAnimation("headscratch");
            entityPlayer.AnimManager.StartAnimation("cough");
        }

    }

    private static NormalRandom normalRandom = new(0xB0BA);
    private static void PrintSniffAlert(ICoreClientAPI capi)
    {
        EntityPlayer entityPlayer = capi.World.Player.Entity;

        if (entityPlayer.GetBehavior<EntityBehaviorStinky>() is not EntityBehaviorStinky entityBehaviorStinky)
        {
            return;
        }

        string stinkinessStr;
        if (Buff.ActiveOnEntity(entityPlayer, Constants.PERFUME_BUFF_KEY))
        {
            stinkinessStr = "\n" + Lang.Get($"bathtime:stinkiness-level-perfume");
        }
        else
        {
            int randInt = normalRandom.NextInt(5);
            stinkinessStr = entityBehaviorStinky.Stinkiness switch
            {
                > 0.9 => Lang.Get($"bathtime:stinkiness-level-extreme-{randInt}"),
                > 0.75 => Lang.Get($"bathtime:stinkiness-level-high-{randInt}"),
                > 0.5 => Lang.Get($"bathtime:stinkiness-level-medium-{randInt}"),
                > 0.25 => Lang.Get($"bathtime:stinkiness-level-low-{randInt}"),
                _ => Lang.Get($"bathtime:stinkiness-level-clean-{randInt}"),
            };
        }

        int[] msgColor = ColorUtil.Hsv2RgbInts(
            Constants.hsvaStinkBaseColor[0],
            (int)(Constants.hsvaStinkBaseColor[1] * entityBehaviorStinky.Stinkiness),
            Constants.hsvaStinkBaseColor[2]
        );

        capi.ShowChatMessage($"<font color=\"#{msgColor[0]:X}{msgColor[1]:X}{msgColor[2]:X}\">{stinkinessStr}</font>");
    }
}
