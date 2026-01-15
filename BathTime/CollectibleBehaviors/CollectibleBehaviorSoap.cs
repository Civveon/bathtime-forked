using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

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
    protected override bool IsValidTarget(Entity targetEntity)
    {
        var stinkBehavior = targetEntity.GetBehavior<EntityBehaviorStinky>();
        var toiletryModifier = stinkBehavior?.GetRateModifier<SoapBuff>();
        if (toiletryModifier is not null) return !toiletryModifier.IsActive && EntityBehaviorStinky.IsBathing(targetEntity);
        else return false;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        Entity targetEntity = byEntity;
        if (entitySel is not null) targetEntity = entitySel.Entity;
        if (!EntityBehaviorStinky.IsBathing(targetEntity))
        {
            handling = EnumHandling.PassThrough;
            handHandling = EnumHandHandling.NotHandled;
            return;
        }
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, ref EnumHandling handling)
    {
        Entity targetEntity = byEntity;
        if (entitySel is not null) targetEntity = entitySel.Entity;
        if (!EntityBehaviorStinky.IsBathing(targetEntity))
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
        return [
            new()
            {
                ActionLangCode = "bathtime:heldhelp-soap",
                MouseButton = EnumMouseButton.Right,
            }
        ];
    }

}
