using System;
using Vintagestory.API.Common.Entities;

namespace BathTime;

class StinkRateModifierHygeine : IStinkyRateModifier
{
    Entity entity;

    public bool IsActive => true;

    public double stinkyPriority => 0.5;

    public double ModifyRate(double rateMultplier)
    {
        return Math.Clamp(2.0 - entity.Stats.GetBlended(Constants.PERSONAL_HYGEINE_KEY), 0, 2);
    }

    public StinkRateModifierHygeine(Entity entity)
    {
        this.entity = entity;
    }
}