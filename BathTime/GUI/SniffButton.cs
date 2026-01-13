using System.Collections.Generic;
using System.Reflection.Emit;
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

        codeMatcher.MatchStartForward(
            new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(GuiComposer), "Compose", [typeof(bool)]))
        );

        codeMatcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(CharacterExtraDialogs), "capi")),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(CharacterExtraDialogs), "Composers")),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CharacterExtraDialogsPatch), nameof(ComposeSniffButton)))
        );

        return codeMatcher.InstructionEnumeration();
    }

    private static void ComposeSniffButton(CharacterExtraDialogs __instance, ICoreClientAPI capi, DlgComposers composers)
    {
        if (composers is null) return;
        if (!composers.ContainsKey("playerstats")) return;
        if (!composers.ContainsKey("environment")) return;

        ElementBounds leftDlgBounds = composers["playercharacter"].Bounds;
        ElementBounds botDlgBounds = composers["environment"].Bounds;

        ElementBounds leftColumnBounds = ElementBounds.Fixed(0, 25, 90, 20);
        ElementBounds rightColumnBounds = ElementBounds.Fixed(120, 30, 120, 8);

        ElementBounds leftColumnBoundsW = ElementBounds.Fixed(0, 0, 140, 20);
        ElementBounds rightColumnBoundsW = ElementBounds.Fixed(165, 0, 120, 20);

        double b = botDlgBounds.InnerHeight / RuntimeEnv.GUIScale + 10;

        ElementBounds bgBounds = ElementBounds
            .Fixed(0, 0, 130 + 100 + 5, leftDlgBounds.InnerHeight / RuntimeEnv.GUIScale - GuiStyle.ElementToDialogPadding - 20 + b)
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
        ;

        ElementBounds dialogBounds = bgBounds
                .ForkBoundingParent()
                .WithAlignment(EnumDialogArea.LeftMiddle)
                .WithFixedAlignmentOffset((leftDlgBounds.renderX + leftDlgBounds.OuterWidth + 10) / RuntimeEnv.GUIScale, b / 2)
        ;

        GuiComposer playerstatsComposer = composers["playerstats"];
        playerstatsComposer.AddSmallButton(
            Lang.Get("bathtime:sniff"),
            () =>
            {
                PlaySniffAnimation(capi);
                PrintSniffAlert(capi);
                return true;
            },
            dialogBounds.WithAlignment(EnumDialogArea.LeftBottom).WithFixedAlignmentOffset(0, 40).WithFixedSize(180, 40)
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

    private static void PrintSniffAlert(ICoreClientAPI capi)
    {
        EntityPlayer entityPlayer = capi.World.Player.Entity;

        if (entityPlayer.GetBehavior<EntityBehaviorStinky>() is not EntityBehaviorStinky entityBehaviorStinky)
        {
            return;
        }

        string stinkinessStr = entityBehaviorStinky.Stinkiness switch
        {
            > 0.9 => Lang.Get("bathtime:stinkiness-level-extreme"),
            > 0.75 => Lang.Get("bathtime:stinkiness-level-high"),
            > 0.5 => Lang.Get("bathtime:stinkiness-level-medium"),
            > 0.25 => Lang.Get("bathtime:stinkiness-level-low"),
            _ => Lang.Get("bathtime:stinkiness-level-clean"),
        };

        int[] msgColor = ColorUtil.Hsv2RgbInts(
            Constants.hsvaStinkBaseColor[0],
            (int)(Constants.hsvaStinkBaseColor[1] * entityBehaviorStinky.Stinkiness),
            Constants.hsvaStinkBaseColor[2]
        );

        capi.ShowChatMessage($"<font color=\"#{msgColor[0]:X}{msgColor[1]:X}{msgColor[2]:X}\">{stinkinessStr}</font>");
    }
}
