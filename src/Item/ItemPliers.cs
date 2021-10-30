using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace CrazyMachines
{
    public class ItemPliers : Item
    {
        SkillItem[] toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreClientAPI capi)
            {
                toolModes = ObjectCacheUtil.GetOrCreate(api, "plierToolModes", () =>
                {
                    SkillItem[] modes = new SkillItem[6];

                    foreach (BlockFacing face in BlockFacing.ALLFACES) modes[face.Index] = new SkillItem() { Code = new AssetLocation("crazymachines:" + face.Code), Name = Lang.Get("crazymachines:wireinfo-" + face.Code) }.WithIcon(capi, (cr, x, y, w, h, c) => PortSymbol(cr, x, y, w, h, c, face));

                    return modes;
                });
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            for (int i = 0; toolModes != null && i < toolModes.Length; i++)
            {
                toolModes[i]?.Dispose();
            }
        }

        public static void PortSymbol(Context cr, int x, int y, float width, float height, double[] rgba, BlockFacing face)
        {
            string text = string.Empty;
            TextDrawUtil stampy = new TextDrawUtil();

            Pattern pattern = null;
            Matrix matrix = cr.Matrix;

            cr.Save();

            ElementBounds bounds = ElementBounds.Fixed((int)width, (int)height);

            CairoFont font = CairoFont.WhiteMediumText();
            font.StrokeColor = ColorUtil.BlackArgbDouble;
            font.FontWeight = FontWeight.Bold;

            text = face.Code[0].ToString().ToUpper();

            

            cr.Operator = Operator.Over;
            pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.SetSource(pattern);

            cr.NewPath();
            
            cr.Tolerance = 0.1;
            cr.Antialias = Antialias.Default;
            cr.FillRule = FillRule.Winding;

            font.UnscaledFontsize = 4000000;
            font.AutoFontSize(text, bounds);

            stampy.DrawTextLine(cr, font, text, width/2, height/2);

            cr.FillPreserve();
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            return toolModes;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return slot.Itemstack.Attributes.GetInt("toolMode");
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }


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
                WireSystem modWiring = byEntity.World.Api.ModLoader.GetModSystem<WireSystem>();
                
                if (modWiring.IsWired(blockSel.Position))
                {
                    if (byEntity.Controls.Sprint)
                    {
                        ItemStack wires = modWiring.RemoveWires(blockSel.Position);
                        if (wires != null) byEntity.World.SpawnItemEntity(wires, blockSel.Position.ToVec3d().Add(0.5));
                    }
                    else
                    {
                        modWiring.TogglePort(blockSel.Position, slot.Itemstack.Attributes.GetInt("toolMode"));
                    }
                    modWiring.ReconfigureNetwork(blockSel.Position);
                }
                return;
            }

            //base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}
