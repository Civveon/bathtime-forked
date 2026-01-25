using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace BathTime;

public class SoapConfig : IToiletryConfig
{
    public float ApplicationTimeSec { get; set; } = 2;

    public float CooldownTimeHours { get; set; } = 0.5f;

    public float StinkRateReduction { get; set; } = 50f;

    public bool ConsumeOnUse { get; set; } = true;
}

public class CollectibleBehaviorSoap(CollectibleObject collObj) : CollectibleBehaviorToiletry<SoapBuff, SoapConfig>(collObj)
{
    protected bool PlayerBlockSelectionIsValidBath(IWorldAccessor world, IPlayer player)
    {
        return (
            // Player is selecting a liquid container.
            player.CurrentBlockSelection.Block is BlockLiquidContainerBase blockLiquidContainer
            // Liquid container is a valid bath.
            && BlockLiquidContainerPatch.BlockIsValidBath(
                world,
                player.CurrentBlockSelection,
                blockLiquidContainer
            )
        );
    }

    protected bool CanInteract(IWorldAccessor? world, Entity entity, EntitySelection? entitySel)
    {
        if (world is null) return false;
        Entity targetEntity = entity;
        if (entitySel is not null) targetEntity = entitySel.Entity;
        return (
            EntityBehaviorStinky.IsBathing(targetEntity)
            ||
            (
                targetEntity is EntityPlayer entityPlayer
                && PlayerBlockSelectionIsValidBath(world, entityPlayer.Player)
            )
        );
    }

    protected override bool IsValidTarget(Entity targetEntity)
    {
        var stinkBehavior = targetEntity.GetBehavior<EntityBehaviorStinky>();
        var toiletryModifier = stinkBehavior?.GetRateModifier<SoapBuff>();
        if (toiletryModifier is not null) return !toiletryModifier.IsActive && CanInteract(api?.World, targetEntity, null);
        else return false;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (!CanInteract(api?.World, byEntity, entitySel))
        {
            handling = EnumHandling.PassThrough;
            handHandling = EnumHandHandling.NotHandled;
            return;
        }
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, ref EnumHandling handling)
    {
        if (!CanInteract(api?.World, byEntity, entitySel))
        {
            handling = EnumHandling.PassThrough;
            return false;
        }
        return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
    }

    protected override void OnToiletryApply(Entity fromEntity, Entity targetEntity, SoapBuff rateModifier, ItemSlot toiletrySlot)
    {
        rateModifier.stinkRateReduction = config.StinkRateReduction;
        rateModifier.Apply(config.CooldownTimeHours);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        dsc.AppendLine(Lang.Get("bathtime:soap-item-info", $"{config.ApplicationTimeSec:F1} secs", $"{config.CooldownTimeHours:F1} hours", $"{config.StinkRateReduction:F1}"));
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        // I think this only ever runs Client-side but best to be sure.
        if (api?.Side == EnumAppSide.Client)
        {
            var capi = (ICoreClientAPI)api;
            if (!CanInteract(api.World, capi.World.Player.Entity, null))
            {
                return [
                    new()
                    {
                        ActionLangCode = "bathtime:heldhelp-soap-notbathing",
                        MouseButton = EnumMouseButton.None,
                    }
                ];
            }
        }

        return [
            new()
            {
                ActionLangCode = "bathtime:heldhelp-soap",
                MouseButton = EnumMouseButton.Right,
            }
        ];
    }
}
