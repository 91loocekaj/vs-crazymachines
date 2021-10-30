using Vintagestory.API.Common;

namespace CrazyMachines
{
    public class ItemWire : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || !byEntity.Controls.Sneak) return;

            IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            handling = EnumHandHandling.PreventDefault;
            
            if (byEntity.Api.Side == EnumAppSide.Server)
            {
                Block ground = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);

                if (ground.GetBehavior<BlockBehaviorWire>()?.CanWire() != true) return;

                WireSystem modWiring = byEntity.World.Api.ModLoader.GetModSystem<WireSystem>();
                modWiring.WireBlock(blockSel.Position, slot, ground.BlockId);
                modWiring.ReconfigureNetwork(blockSel.Position);
                return;
            }

            //base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}
