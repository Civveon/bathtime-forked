using System;
using System.Collections.Generic;
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
    private static readonly ConcurrentSmallDictionary<string, long> CallbackIdByPlayer = [];

    [HarmonyPatch(typeof(BlockLiquidContainerBase))]
    public static class BlockLiquidContainerBaseInteractStartPatch
    {
        [HarmonyPatch(nameof(BlockLiquidContainerBase.OnBlockInteractStart))]
        public static void Postfix(ref bool __result, BlockLiquidContainerBase __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Used to prevent players spamming the first bathing tick. We clean up entries
            // in the dictionary with a callback delay after the interaction stops.
            if (BathingSecondsByPlayer.ContainsKey(byPlayer.PlayerUID + world.Side.ToString())) return;

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
                ApplyBathing(__instance, blockSel, byPlayer);
                BathingSecondsByPlayer.Add(byPlayer.PlayerUID + world.Side.ToString(), 0);
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
    public static class BlockLiquidContainerBaseInteractionHelpPatch
    {
        [HarmonyPatch(nameof(Block.GetPlacedBlockInteractionHelp))]
        public static void Postfix(ref WorldInteraction[] __result, Block __instance, IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (__instance is not BlockLiquidContainerBase) return;
            if (world.Side == EnumAppSide.Server) return;

            ICoreClientAPI capi = (ICoreClientAPI)world.Api;

            if (BlockIsValidBath(world, selection, (BlockLiquidContainerBase)__instance))
            {
                __result = __result.Append(new WorldInteraction
                {
                    ActionLangCode = "bathtime:blockhelp-bath-rightclick",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true,
                });

                __result = __result.Append(new WorldInteraction
                {
                    ActionLangCode = "bathtime:blockhelp-bath-soap",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = ObjectCacheUtil.GetOrCreate(
                        world.Api,
                        "liquidContainerBaseSoap",
                        delegate
                        {
                            List<ItemStack> list = new();
                            foreach (CollectibleObject collectible in world.Collectibles)
                            {
                                if (collectible.HasBehavior<CollectibleBehaviorSoap>())
                                {
                                    List<ItemStack> handBookStacks = collectible.GetHandBookStacks(capi);
                                    if (handBookStacks != null)
                                    {
                                        list.AddRange(handBookStacks);
                                    }
                                }
                            }

                            ItemStack[] lstacks = [.. list];
                            return lstacks;
                        }
                    )
                });
            }
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

        // Make the player wet.
        if (byPlayer.Entity.GetBehavior<EntityBehaviorBodyTemperature>() is EntityBehaviorBodyTemperature bodyTemperature)
        {
            bodyTemperature.Wetness = 1;
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

        // Register callback to forget player so they can start bathing again.
        if (CallbackIdByPlayer.ContainsKey(byPlayer.PlayerUID + api.World.Side.ToString()))
        {
            api.Event.UnregisterCallback(CallbackIdByPlayer.Get(byPlayer.PlayerUID + api.World.Side.ToString()));
        }
        CallbackIdByPlayer.Remove(byPlayer.PlayerUID + api.World.Side.ToString());
        long callbackId = api.Event.RegisterCallback(
            (_) =>
            {
                if (byPlayer is null) return;
                EntityBehaviorStinky? entityBehaviorStinky = byPlayer.Entity.GetBehavior<EntityBehaviorStinky>();
                if (entityBehaviorStinky is null) return;
                BathingSecondsByPlayer.Remove(byPlayer.PlayerUID + api.World.Side.ToString());

                // Disable bathing override flag.
                entityBehaviorStinky.isBathingOverride = false;
            },
            (int)(1000 * GetSecondsToBathe(byPlayer.Entity))
        );
        CallbackIdByPlayer.Add(byPlayer.PlayerUID + api.World.Side.ToString(), callbackId);
    }

    public static bool BlockIsValidBath(IWorldAccessor world, BlockSelection blockSel, BlockLiquidContainerBase blockLiquidContainer)
    {
        return (
            (
                blockLiquidContainer is BlockLiquidContainerTopOpened
                || (
                    world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBarrel beb
                    && beb.Sealed == false
                )
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
