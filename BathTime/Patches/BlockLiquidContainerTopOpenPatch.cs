using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace BathTime;


public partial class BathtimeConfig
{
    public float secondsToBatheInBucketPortion { get; set; } = 2.1f;
}

public static class BlockLiquidContainerPatch
{
    private static readonly ConcurrentSmallDictionary<string, float> BathingSecondsByPlayer = [];

    [HarmonyPatch(typeof(BlockLiquidContainerBase))]
    public static class BlockLiquidContainerBaseInteractStartPatch
    {
        [HarmonyPatch(nameof(BlockLiquidContainerBase.OnBlockInteractStart))]
        public static void Postfix(ref bool __result, BlockLiquidContainerBase __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Check player is stinky, sneaking with empty hand, and block is valid.
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (hotbarSlot is null) return;
            bool shiftHeld = byPlayer.WorldData.EntityControls.ShiftKey;

            if (
                hotbarSlot.Empty &&
                shiftHeld &&
                BlockIsValidBath(world, blockSel, __instance) &&
                byPlayer.Entity.HasBehavior<EntityBehaviorStinky>()
            )
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Block))]
    public static class BlockLiquidContainerBaseInteractStepPatch
    {
        [HarmonyPatch(nameof(Block.OnBlockInteractStep))]
        public static void Postfix(ref bool __result, Block __instance, float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (__instance is not BlockLiquidContainerBase) return;

            // Check player has not released sneaking or filled their hand, and block is still valid.
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (hotbarSlot is null) return;
            bool shiftHeld = byPlayer.WorldData.EntityControls.ShiftKey;

            if (
                hotbarSlot.Empty &&
                shiftHeld &&
                BlockIsValidBath(world, blockSel, (BlockLiquidContainerBase)__instance) &&
                byPlayer.Entity.HasBehavior<EntityBehaviorStinky>()
            )
            {
                float secondsToBathe = GetSecondsToBathe(byPlayer.Entity);

                // If we've ticked over a multiple of secondsToBathe, apply bathing action.
                float prevSecondsUsed = BathingSecondsByPlayer.Get(byPlayer.PlayerUID + world.Side.ToString());
                if (Math.Floor(prevSecondsUsed / secondsToBathe) < Math.Floor(secondsUsed / secondsToBathe))
                {
                    ApplyBathing((BlockLiquidContainerBase)__instance, blockSel, byPlayer);
                }

                BathingSecondsByPlayer.Add(byPlayer.PlayerUID + world.Side.ToString(), secondsUsed);

                __result = true;
                return;
            }
        }
    }

    [HarmonyPatch(typeof(Block))]
    public static class BlockLiquidContainerBaseInteractStopPatch
    {
        [HarmonyPatch(nameof(Block.OnBlockInteractStop))]
        public static void Postfix(Block __instance, float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (__instance is not BlockLiquidContainerBase) return;
            EntityBehaviorStinky? entityBehaviorStinky = byPlayer.Entity.GetBehavior<EntityBehaviorStinky>();
            if (entityBehaviorStinky is null) return;

            // Disable bathing override flag.
            entityBehaviorStinky.isBathingOverride = false;
        }
    }

    private static void ApplyBathing(BlockLiquidContainerBase container, BlockSelection blockSel, IPlayer byPlayer)
    {
        ItemStack? contentStack = container.GetContent(blockSel.Position);
        if (contentStack is null) return;
        WaterTightContainableProps? props = BlockLiquidContainerBase.GetContainableProps(contentStack);
        if (props is null) return;
        EntityBehaviorStinky? entityBehaviorStinky = byPlayer.Entity.GetBehavior<EntityBehaviorStinky>();
        if (entityBehaviorStinky is null) return;

        var api = byPlayer.Entity.Api;

        // Set override flag to mark this entity as bathing.
        entityBehaviorStinky.isBathingOverride = true;

        // Remove a portion from the container.
        if (api.Side == EnumAppSide.Server)
        {
            container.TryTakeContent(blockSel.Position, (int)props.ItemsPerLitre);
        }

        // Play bathing animation.
        if (api.Side == EnumAppSide.Client)
        {
            ICoreClientAPI capi = (ICoreClientAPI)api;
            if (capi.World.Player.CameraMode == EnumCameraMode.FirstPerson)
            {
                if (!byPlayer.Entity.AnimManager.IsAnimationActive("headscratch-fp", "cough-fp"))
                {
                    byPlayer.Entity.AnimManager.StartAnimation("headscratch-fp");
                    byPlayer.Entity.AnimManager.StartAnimation("cough-fp");
                }
            }
            else
            {
                if (!byPlayer.Entity.AnimManager.IsAnimationActive("headscratch", "cough"))
                {
                    byPlayer.Entity.AnimManager.StartAnimation("headscratch");
                    byPlayer.Entity.AnimManager.StartAnimation("cough");
                }
            }
        }
        else
        {
            if (!byPlayer.Entity.AnimManager.IsAnimationActive("headscratch", "cough"))
            {
                byPlayer.Entity.AnimManager.StartAnimation("headscratch");
                byPlayer.Entity.AnimManager.StartAnimation("cough");
            }
        }
    }

    public static bool BlockIsValidBath(IWorldAccessor world, BlockSelection blockSel, BlockLiquidContainerBase blockLiquidContainer)
    {
        return (
            blockLiquidContainer is BlockLiquidContainerTopOpened
            || (
                world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBarrel beb
                && beb.Sealed == false
            )
            && (
                blockLiquidContainer.GetContent(blockSel.Position) is ItemStack contents
                && contents.Item.Code.Path.Contains("water")
            )
        );
    }

    private static float GetSecondsToBathe(Entity byEntity)
    {
        ICoreAPI api = byEntity.Api;
        float secondsToBathe;
        if (api.Side == EnumAppSide.Server)
        {
            BathtimeConfig config = BathtimeBaseConfig<BathtimeConfig>.LoadStoredConfig(api);
            secondsToBathe = config.secondsToBatheInBucketPortion;
        }
        else
        {
            secondsToBathe = byEntity.GetFloatAttribute(Constants.SECONDS_TO_BATHE_KEY, 1);
        }
        return secondsToBathe;
    }
}
