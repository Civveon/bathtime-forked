using Vintagestory.API.Common.Entities;

namespace BathTime;

public class PerfumeBuff : Buff, IStinkyRateModifier
{
    public double stinkyPriority => Constants.RATE_MULTIPLIER_MULTIPLICATIVE_PRIORITY;

    public double ModifyRate(double rateMultplier)
    {
        return rateMultplier * 0.9;
    }

    public bool IsActive => durationHours > 0;

    public PerfumeBuff(Entity entity) : base(entity, Constants.PERFUME_BUFF_KEY, 300)
    {
        // Alchemy compatibility.
        entity.WatchedAttributes.RegisterModifiedListener(
            "scentmaskpotionid",
            () =>
            {
                if (entity.WatchedAttributes.HasAttribute("scentmaskpotionid"))
                {
                    // This matches the default conversion of the duration
                    // of the alchemy potion to in-game hours. However, Alchemy
                    // uses real-world time NOT in-game time, so they will not
                    // necessarily match up. It also doesn't respect
                    // time skips. Oh well.
                    Apply(5);
                }
            }
        );
    }
}