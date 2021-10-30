using Electricity.API;
using qptech.src;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace CrazyMachines
{
    public class BlockEntityWireJunction : BEElectric, IWireEndPoint
    {
        bool isInsulated = false;
        IElectricity[] WireNetwork;
        WireSystem modWiring;
        public override void Initialize(ICoreAPI api)
        {
            modWiring = api.ModLoader.GetModSystem<WireSystem>();
            
            if (api.Side == EnumAppSide.Server && WireNetwork == null)
            {
                WireNetwork = modWiring.FindNetwork(Pos, 2);
            }
            
            base.Initialize(api);
            if (Block == null) { return; }
            if (Block.Attributes == null) { return; }
            isInsulated = Block.Attributes["isInsulated"].AsBool(isInsulated);
            RegisterGameTickListener((dt) => { if (api.Side == EnumAppSide.Server) UpdateNetwork(); }, 3000);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            
            modWiring = Api.ModLoader.GetModSystem<WireSystem>();

            if (Api.Side == EnumAppSide.Server)
            {
                if (WireNetwork == null) WireNetwork = modWiring.FindNetwork(Pos, 2);
                
                modWiring.ReconfigureNetwork(Pos, 2);
            }
        }

        public virtual void EntityCollide(Entity entity)
        {
            if (!isInsulated && Capacitor > 0 && IsPowered) { entity.IsOnFire = true; }
        }

        public void UpdateNetwork()
        {
            WireNetwork = modWiring.FindNetwork(Pos, 2);
        }

        public override void DistributePower()
        {
            // bunch of checks to see if we can give power
            if (Capacitor == 0) { return; }
            if (usedconnections == null) { usedconnections = new List<IElectricity>(); }
            if (!isOn) { return; } //can't generator power if off
            if (outputConnections == null) { return; } //nothing hooked up
            if (outputConnections.Count == 0) { return; }

            //figure out who needs power
            List<IElectricity> tempconnections = new List<IElectricity>();
            int powerreq = 0;
            foreach (IElectricity ie in outputConnections)
            {
                int np = ie.NeedPower();
                if (np == 0) { continue; }
                powerreq += np;
                tempconnections.Add(ie);
            }

            if (WireNetwork != null && WireNetwork.Length > 0)
            {
                foreach(IElectricity end in WireNetwork)
                {
                    int np = end.NeedPower();
                    if (np == 0) { continue; }
                    powerreq += np;
                    tempconnections.Add(end);
                }
            }

            if (powerreq == 0) { return; } //Don't need to distribute any power
            bool gavepower = false;
            //cap the powerrequest to our max TF, by the number of requests
            powerreq = Math.Min(powerreq, tempconnections.Count * maxFlux);
            //distribute what power we can
            //If we have more power than is requested, just go through and give power
            if (Capacitor >= powerreq)
            {
                foreach (IElectricity ie in tempconnections)
                {
                    int offer = ie.ReceivePacketOffer(this, Math.Min(Capacitor, maxFlux));
                    if (offer > 0) { ChangeCapacitor(-offer); gavepower = true; }
                }
                if (gavepower) { MarkDirty(true); }
                return;
            }

            //Not enough power to go around, have to divide it up
            int eachavail = Capacitor / tempconnections.Count;
            int leftover = Capacitor % tempconnections.Count; //remainder
            foreach (IElectricity ie in tempconnections)
            {
                int offer = ie.ReceivePacketOffer(this, eachavail);
                if (offer == 0) { continue; }
                gavepower = true;
                ChangeCapacitor(-offer);
                if (leftover > 0)
                {
                    offer = ie.ReceivePacketOffer(this, leftover);
                    if (offer > 0)
                    {
                        leftover -= offer;
                        ChangeCapacitor(-offer);
                    }
                }
            }
            if (gavepower) { MarkDirty(true); }

        }
    }
}
