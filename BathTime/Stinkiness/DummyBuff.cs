using Vintagestory.API.Common.Entities;

namespace BathTime;

/// <summary>
/// Dummy buff attached to toiletries that do not have entity effects.
/// </summary>
public class DummyBuff : Buff, IStinkyRateModifier
{
    public double stinkyPriority => Constants.RATE_MULTIPLIER_MULTIPLICATIVE_PRIORITY;

    public double ModifyRate(double rateMultplier)
    {
        return rateMultplier;
    }

    public bool IsActive => false;

    public DummyBuff(Entity entity) : base(entity, Constants.DUMMY_BUFF_KEY, 300)
    {
    }

    public override void Apply(double durationHours)
    {
        return;
    }

    protected override void onGameTick(float dt)
    {
        return;
    }

    protected override void OnEnd()
    {
        return;
    }
}