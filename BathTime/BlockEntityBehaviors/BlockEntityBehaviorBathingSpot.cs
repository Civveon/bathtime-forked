using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace BathTime;

public class BlockEntityBehaviorBathingSpotConfig
{
    public float MinBathingSpotVolume { get; set; } = 0;
}

public class BlockEntityBehaviorBathingSpot : BlockEntityBehavior
{
    public BlockEntityBehaviorBathingSpot(BlockEntity blockentity) : base(blockentity)
    {
    }

    protected BlockEntityBehaviorBathingSpotConfig config { get; set; } = new();

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        config = properties.AsObject<BlockEntityBehaviorBathingSpotConfig>();
    }

    private static float GetLiquidVolume(BlockEntity be)
    {
        if (be is IBlockEntityContainer container)
        {
            float volume = 0;
            foreach (var slot in container.Inventory)
            {
                if (slot is not null && !slot.Empty && slot.Itemstack.Collectible.Code.Path.Contains("water"))
                {
                    volume += slot.Itemstack.StackSize;
                }
            }
            return volume;
        }
        return 0;
    }

    public bool IsBathingLiquid()
    {
        return GetLiquidVolume(Blockentity) >= config.MinBathingSpotVolume;
    }

    public static bool IsBathingSpotAtPos(IWorldAccessor worldAccessor, BlockPos blockPos)
    {
        if (worldAccessor.BlockAccessor.GetBlock(blockPos) is BlockMultiblock blockMultiblock)
        {
            blockPos = blockMultiblock.GetControlBlockPos(blockPos);
        }
        var be = worldAccessor.BlockAccessor.GetBlockEntity(blockPos);
        if (be is not null)
        {
            var behavior = be.GetBehavior<BlockEntityBehaviorBathingSpot>();
            if (behavior is not null && behavior.IsBathingLiquid())
            {
                return true;
            }
        }

        return false;
    }
}
