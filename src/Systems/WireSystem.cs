using Electricity.API;
using ProtoBuf;
using qptech.src;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace CrazyMachines
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class WireInfo
    {
        public int Block;
        public string WireCode;
        public int WireAmount;
        public bool N, S, E, W, U, D;

        public ItemStack Wire(IWorldAccessor world)
        {
            AssetLocation wireCode = new AssetLocation(WireCode);
            CollectibleObject obj = (world.GetItem(wireCode) as CollectibleObject) ?? (world.GetBlock(wireCode) as CollectibleObject);

            if (obj == null) return null;

            return new ItemStack(obj, WireAmount);
        }

        public bool OutletOn(int face)
        {
            switch (face)
            {
                case 0:
                    return N;
                case 1:
                    return E;
                case 2:
                    return S;
                case 3:
                    return W;
                case 4:
                    return U;
                case 5:
                    return D;
            }

            return false;
        }

        public bool OutletOn(BlockFacing face)
        {
            return OutletOn(face.Index);
        }

        public void SetOutlet(int face, bool value)
        {
            switch (face)
            {
                case 0:
                    N = value;
                    break;
                case 1:
                    E = value;
                    break;
                case 2:
                    S = value;
                    break;
                case 3:
                    W = value;
                    break;
                case 4:
                    U = value;
                    break;
                case 5:
                    D = value;
                    break;
            }
        }

        public void SetOutlet(BlockFacing face, bool value)
        {
            SetOutlet(face.Index, value);
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ChunkWiringData
    {
        public byte[] Data;
        public int chunkX, chunkY, chunkZ;
    }

    public class WireChunk
    {
        public IWorldChunk Chunk;
        public Dictionary<int, WireInfo> Wires;
        public BlockPos starter;

        public int X;
        public int Y;
        public int Z;

        public WireChunk(IWorldChunk newChunk, Dictionary<int, WireInfo> newWires, BlockPos pos, int chunksize)
        {
            Chunk = newChunk;
            Wires = newWires;
            starter = pos;
            X = pos.X / chunksize;
            Y = pos.Y / chunksize;
            Z = pos.Z / chunksize;
        }

        public bool Compare(BlockPos pos, int chunksize)
        {
            return pos.X / chunksize == X && pos.Y / chunksize == Y && pos.Z / chunksize == Z;
        }
    }

    public interface IWireEndPoint
    {
        void UpdateNetwork();
    }

    public class WireSystem : ModSystem
    {
        ICoreAPI api;

        IClientNetworkChannel clientChannel;
        IServerNetworkChannel serverChannel;

        int[] EndPointIds;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            api.RegisterBlockBehaviorClass("Wiring", typeof(BlockBehaviorWire));
            api.RegisterItemClass("ItemWire", typeof(ItemWire));
            api.RegisterItemClass("ItemPliers", typeof(ItemPliers));
            api.RegisterBlockEntityClass("WireJunction", typeof(BlockEntityWireJunction));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            clientChannel = api.Network
                .RegisterChannel("wiring")
                .RegisterMessageType(typeof(ChunkWiringData))
                .SetMessageHandler<ChunkWiringData>(onChunkData)
            ;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, addWireBehavior);

            serverChannel = api.Network
                .RegisterChannel("wiring")
                .RegisterMessageType(typeof(ChunkWiringData))
            ;
        }

        private void addWireBehavior()
        {
            List<int> endpoints = new List<int>();

            foreach (Block block in api.World.Blocks)
            {
                if (CanWire(block))
                {
                    block.BlockBehaviors = block.BlockBehaviors.Append(new BlockBehaviorWire(block));
                    block.CollectibleBehaviors = block.CollectibleBehaviors.Append(new BlockBehaviorWire(block));
                }

                if (block.Class == "BlockJunction")
                {
                    endpoints.Add(block.BlockId);
                }

                EndPointIds = endpoints.ToArray();               
            }

            //System.Diagnostics.Debug.WriteLine("There are " + EndPointIds.Length + " in the system");

        }

        private void onChunkData(ChunkWiringData msg)
        {
            IWorldChunk chunk = api.World.BlockAccessor.GetChunk(msg.chunkX, msg.chunkY, msg.chunkZ);
            if (chunk != null)
            {
                chunk.SetModdata("wiring", msg.Data);
            }
        }

        Dictionary<int, WireInfo> getOrCreateWiresAt(BlockPos pos)
        {
            byte[] data;

            IWorldChunk chunk = api.World.BlockAccessor.GetChunkAtBlockPos(pos);
            if (chunk == null) return null;

            data = chunk.GetModdata("wiring");

            Dictionary<int, WireInfo> wiresOfChunk = null;

            if (data != null)
            {
                try
                {
                    wiresOfChunk = SerializerUtil.Deserialize<Dictionary<int, WireInfo>>(data);
                }
                catch (Exception)
                {
                    wiresOfChunk = new Dictionary<int, WireInfo>();
                }
            }
            else
            {
                wiresOfChunk = new Dictionary<int, WireInfo>();
            }

            return wiresOfChunk;
        }

        void saveWires(Dictionary<int, WireInfo> wires, BlockPos pos)
        {
            int chunksize = api.World.BlockAccessor.ChunkSize;
            int chunkX = pos.X / chunksize;
            int chunkY = pos.Y / chunksize;
            int chunkZ = pos.Z / chunksize;

            byte[] data = SerializerUtil.Serialize(wires);

            IWorldChunk chunk = api.World.BlockAccessor.GetChunk(chunkX, chunkY, chunkZ);
            chunk.SetModdata("wiring", data);

            // Todo: Send only to players that have this chunk in their loaded range
            serverChannel?.BroadcastPacket(new ChunkWiringData() { chunkX = chunkX, chunkY = chunkY, chunkZ = chunkZ, Data = data });
        }

        public ItemStack RemoveWires(BlockPos pos)
        {
            Dictionary<int, WireInfo> wiresOfChunk = getOrCreateWiresAt(pos);
            if (wiresOfChunk == null) return null;

            int index3d = toLocalIndex(pos);
            if (!wiresOfChunk.ContainsKey(index3d)) return null;

            ItemStack wire = wiresOfChunk[index3d].Wire(api.World);

            if (wiresOfChunk.Remove(index3d))
            {
                saveWires(wiresOfChunk, pos);
                return wire;
            }

            return null;
        }

        public WireInfo GetWire(BlockPos pos)
        {
            Dictionary<int, WireInfo> wiresOfChunk = getOrCreateWiresAt(pos);
            if (wiresOfChunk == null) return null;

            int index3d = toLocalIndex(pos);
            if (!wiresOfChunk.ContainsKey(index3d)) return null;
            
            return wiresOfChunk[index3d];
        }

        public bool IsWired(BlockPos pos)
        {
            IWorldChunk chunk = api.World.BlockAccessor.GetChunkAtBlockPos(pos);
            if (chunk == null) return false;
            Dictionary<int, WireInfo> wiresOfChunk = getOrCreateWiresAt(pos);

            if (wiresOfChunk == null) return false;
            int index3d = toLocalIndex(pos);

            return wiresOfChunk.ContainsKey(index3d);
        }

        public void TogglePort(BlockPos pos, int face, bool choose = false, bool choice = false)
        {
            Dictionary<int, WireInfo> wiresOfChunk = getOrCreateWiresAt(pos);

            if (wiresOfChunk == null) return;
            int index3d = toLocalIndex(pos);
            if (!wiresOfChunk.ContainsKey(index3d)) return;

            bool on = wiresOfChunk[index3d].OutletOn(face);

            if (on) wiresOfChunk[index3d].SetOutlet(face, false); else wiresOfChunk[index3d].SetOutlet(face, true);

            saveWires(wiresOfChunk, pos);
        }

        public void WireBlock(BlockPos pos, ItemSlot wireInv, int blockId)
        {
            int? takeOut = wireInv.Itemstack?.Collectible.Attributes?["wireAmount"].AsInt();
            if (takeOut == null || takeOut < 1 || takeOut > wireInv.Itemstack.StackSize) return;

            Dictionary<int, WireInfo> wiresOfChunk = getOrCreateWiresAt(pos);
            if (wiresOfChunk == null) return;
            int index3d = toLocalIndex(pos);
            if (wiresOfChunk.ContainsKey(index3d)) return;
            string wireCode = wireInv.Itemstack.Collectible.Code.Domain + ":" + wireInv.Itemstack.Collectible.Code.Path;

            wiresOfChunk[index3d] = new WireInfo() { Block = blockId, WireAmount = (int)takeOut, WireCode = wireCode, N = true, D = true, E = true, S = true, U = true, W = true};
            wireInv.TakeOut((int)takeOut);
            wireInv.MarkDirty();
            saveWires(wiresOfChunk, pos);
        }

        public bool CanWire(Block check)
        {
            bool solidBlock = true;

            for (int i = 0; i < check.SideOpaque.Length; i++) solidBlock &= check.SideOpaque[i];
            if (solidBlock) for (int i = 0; i < check.SideSolid.Length; i++) solidBlock &= check.SideSolid[i];

            return solidBlock;
        }

        public IElectricity[] FindNetwork(BlockPos pos, int minRange = -1)
        {
            Queue<Vec4i> checkQueue = new Queue<Vec4i>();
            HashSet<BlockPos> conductPositions = new HashSet<BlockPos>();
            List<IElectricity> endpoints = new List<IElectricity>();
            List<WireChunk> chunks = new List<WireChunk>();

            checkQueue.Enqueue(new Vec4i(pos.X, pos.Y, pos.Z, 0));
            conductPositions.Add(pos);
            IWorldChunk starterchunk = api.World.BlockAccessor.GetChunkAtBlockPos(pos);
            starterchunk.Unpack();
            chunks.Add(new WireChunk(starterchunk, getOrCreateWiresAt(pos), pos, api.World.BlockAccessor.ChunkSize));

            BlockFacing[] faces = BlockFacing.ALLFACES;
            BlockPos curPos = new BlockPos();

            while (checkQueue.Count > 0)
            {
                Vec4i bpos = checkQueue.Dequeue();

                WireChunk parentArea = null;
                curPos.Set(bpos.X, bpos.Y, bpos.Z);
                int pid = toLocalIndex(curPos);
                foreach (WireChunk chunk in chunks)
                {
                    if (chunk.Compare(curPos, api.World.BlockAccessor.ChunkSize))
                    {
                        parentArea = chunk;
                        break;
                    }
                }

                foreach (BlockFacing facing in faces)
                {
                    if (parentArea.Wires.ContainsKey(pid) && !parentArea.Wires[pid].OutletOn(facing)) continue;
                    curPos.Set(bpos.X + facing.Normali.X, bpos.Y + facing.Normali.Y, bpos.Z + facing.Normali.Z);
                    if (conductPositions.Contains(curPos) || bpos.W >= CMConfig.Loaded.MaxNetworkLength) continue;

                    WireChunk localArea = null;

                    foreach (WireChunk chunk in chunks)
                    {
                        if (chunk.Compare(curPos, api.World.BlockAccessor.ChunkSize))
                        {
                            localArea = chunk;
                            break;
                        }
                    }

                    if (localArea == null)
                    {
                        IWorldChunk blockInfo = api.World.BlockAccessor.GetChunkAtBlockPos(curPos);
                        if (blockInfo == null || blockInfo.Disposed) continue;
                        blockInfo.Unpack();
                        localArea = new WireChunk(api.World.BlockAccessor.GetChunkAtBlockPos(curPos), getOrCreateWiresAt(curPos), curPos, api.World.BlockAccessor.ChunkSize);
                        chunks.Add(localArea);
                    }

                    

                    int chunkBid = toLocalIndex(curPos);
                    int range = bpos.W + 1;
                    
                    if (range >= minRange && EndPointIds.Contains(localArea.Chunk.Blocks[chunkBid]))
                    {
                        IElectricity endpoint = api.World.BlockAccessor.GetBlockEntity(curPos) as IElectricity;
                        if (endpoint != null)
                        {
                            endpoints.Add(endpoint);
                            checkQueue.Enqueue(new Vec4i(curPos.X, curPos.Y, curPos.Z, range));
                            conductPositions.Add(curPos);
                        }
                    }
                    else if (localArea.Wires.ContainsKey(chunkBid) && localArea.Wires[chunkBid].OutletOn(facing.Opposite))
                    {
                        checkQueue.Enqueue(new Vec4i(curPos.X, curPos.Y, curPos.Z, range));
                        conductPositions.Add(curPos);
                    }
                }
            }

            return endpoints.ToArray();
        }

        public void ReconfigureNetwork(BlockPos pos, int minRange = -1)
        {
            IElectricity[] endpoints = FindNetwork(pos, minRange);
            //System.Diagnostics.Debug.WriteLine(endpoints.Length);

            foreach(BEElectric endpoint in endpoints)
            {
                (endpoint as IWireEndPoint)?.UpdateNetwork();
            }
        }

        int toLocalIndex(BlockPos pos)
        {
            return MapUtil.Index3d(pos.X % api.World.BlockAccessor.ChunkSize, pos.Y % api.World.BlockAccessor.ChunkSize, pos.Z % api.World.BlockAccessor.ChunkSize, api.World.BlockAccessor.ChunkSize, api.World.BlockAccessor.ChunkSize);
        }

    }
}
