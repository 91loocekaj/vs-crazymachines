using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace CrazyMachines
{
    public class BlockBehaviorWire : BlockBehavior
    {
        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos, ref EnumHandling handling)
        {
            if (world.Side == EnumAppSide.Server)
            {
                WireSystem modWiring = world.Api.ModLoader.GetModSystem<WireSystem>();
                WireInfo wiring = modWiring.GetWire(pos);
                
                if (wiring != null)
                {                  
                    ItemStack wire = modWiring.RemoveWires(pos);
                    if (wire != null) world.SpawnItemEntity(wire, pos.ToVec3d().Add(0.5));
                    modWiring.ReconfigureNetwork(pos);
                }
            }

            base.OnBlockRemoved(world, pos, ref handling);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            CollectibleObject obj = forPlayer?.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible;
            if (obj is ItemPliers || obj is ItemWire)
            {
                WireSystem modWiring = world.Api.ModLoader.GetModSystem<WireSystem>();
                WireInfo wiring = modWiring.GetWire(pos);
                if (wiring != null)
                { 
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(Lang.Get("crazymachines:wireinfo", wiring.Wire(world).GetName()));
                    foreach (BlockFacing face in BlockFacing.ALLFACES)
                    {
                        if (wiring.OutletOn(face)) sb.AppendLine(Lang.Get("crazymachines:wireinfo-" + face.Code));
                    }

                    return sb.ToString();
                }
            }
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public bool CanWire()
        {
            bool solidBlock = true;

            for (int i = 0; i < block.SideOpaque.Length; i++) solidBlock &= block.SideOpaque[i];
            if (solidBlock) for (int i = 0; i < block.SideSolid.Length; i++) solidBlock &= block.SideSolid[i];

            return solidBlock;
        }

        public BlockBehaviorWire(Block block) : base(block)
        {
        }
    }
}
