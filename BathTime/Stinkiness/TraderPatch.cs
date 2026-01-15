using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using BathTime;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

[HarmonyPatch(typeof(EntityBehaviorConversable), nameof(EntityBehaviorConversable.OnInteract))]
public static class TradersFleeStinkyPatch
{
    static bool Prefix(EntityBehaviorConversable __instance, EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (byEntity.GetBehavior<EntityBehaviorStinky>()?.Stinkiness > 0.9)
        {
            handled = EnumHandling.PassThrough;
            if (byEntity.Api.Side == EnumAppSide.Server)
            {
                if
                (
                    (__instance.entity?.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager is AiTaskManager tmgr)
                    && (tmgr.GetTask<AiTaskFleeEntity>() is AiTaskFleeEntity fleeTask)
                )
                {
                    tmgr.StopTasks();
                    tmgr.ExecuteTask(
                        fleeTask,
                        1
                    );
                }
            }
            else
            {
                __instance.TalkUtil.Talk(EnumTalkType.Complain);
            }

            return false;
        }
        else
        {
            return true;
        }
    }
}


[HarmonyPatch(typeof(DlgTalkComponent))]
public static class TradersInsultSmellyPatch
{
    [HarmonyPatch("genText")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> AddLangGetPostfixPayload(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher.MatchStartForward(
            CodeMatch.Calls(() => Lang.Get(default, default))
        )
        .ThrowIfInvalid("Could not find call to Lang.Get")
        .RemoveInstruction()
        .InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldarg_0),
            CodeInstruction.Call(() => LangGetInsertPostfixPayload(default, default, default))
        );

        return codeMatcher.Instructions();
    }

    static string LangGetInsertPostfixPayload(string? key, object[]? _, DlgTalkComponent? dlgTalkComponent)
    {
        if (key is null) throw new NullReferenceException();

        var controller = Traverse.Create(dlgTalkComponent).Field("controller").GetValue();
        EntityPlayer? PlayerEntity = (EntityPlayer?)Traverse.Create(controller).Field("PlayerEntity").GetValue();
        if
        (
            PlayerEntity is not null && (
                key.StartsWith("Welcome back {{playername}}! What can I do for you?")
                || key.StartsWith("Haven't seen you")
            )
        )
        {
            if (!(PlayerEntity.GetBehavior<EntityBehaviorStinky>() is EntityBehaviorStinky entityBehaviorStinky))
            {
                return Lang.Get(key, _);
            }

            string smellResponse = entityBehaviorStinky.Stinkiness switch
            {
                > 0.75 => "\nDamn you smell awful!",
                > 0.5 => "\nYou have an... interesting aroma.",
                > 0.25 => "\nSeems you could use a bath, friend.",
                _ => ""
            };
            return Lang.Get(key) + smellResponse;
        }
        else
        {
            return Lang.Get(key, _);
        }
    }
}