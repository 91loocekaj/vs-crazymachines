using qptech.src;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CrazyMachines
{
    public class BEStabilizer : BEEBaseDevice, IStabilizePOI
    {
        SimpleParticleProperties stabilizeParticles;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            api.ModLoader.GetModSystem<POIRegistry>()?.AddPOI(this);
            api.ModLoader.GetModSystem<StabilizerRegistry>()?.AddPOI(this);

            stabilizeParticles = new SimpleParticleProperties(
                40, 80,
                ColorUtil.ToRgba(50, 220, 220, 220),
                new Vec3d(),
                new Vec3d(),
                new Vec3f(-0.1f, -0.1f, -0.1f),
                new Vec3f(0.1f, 0.1f, 0.1f),
                1.5f,
                0,
                0.5f,
                0.75f,
                EnumParticleModel.Cube
            );

            stabilizeParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -0.6f);
            stabilizeParticles.AddPos.Set(0.1f, 0.1f, 0.1f);
            stabilizeParticles.addLifeLength = 0.5f;
            stabilizeParticles.RandomVelocityChange = true;

            if (Api.Side == EnumAppSide.Client) InitAnim();
        }

        public override void OnTick(float par)
        {
            base.OnTick(par);

            if (Api.Side == EnumAppSide.Client)
            {
                if (animUtil.renderer == null)
                {
                    InitAnim();
                }
                else
                {
                    if (deviceState == enDeviceState.RUNNING)
                    {

                        animUtil.StartAnimation(new AnimationMetaData()
                        {
                            Animation = animationName,
                            Code = animationName,
                            AnimationSpeed = 5f,
                            EaseInSpeed = 2,
                            EaseOutSpeed = 8,
                            Weight = 1,
                            BlendMode = EnumAnimationBlendMode.Average
                        });
                    }
                    else
                    {
                        animUtil.StopAnimation(animationName);
                    }

                }
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            Api.ModLoader.GetModSystem<POIRegistry>()?.RemovePOI(this);
            Api.ModLoader.GetModSystem<StabilizerRegistry>()?.RemovePOI(this);
        }

        protected override void DoRunningParticles()
        {

            int h = 110 + Api.World.Rand.Next(15);
            int v = 100 + Api.World.Rand.Next(50);
            stabilizeParticles.MinPos = Position.AddCopy(-CMConfig.Loaded.MaxStabilizerRadius, -CMConfig.Loaded.MaxStabilizerRadius, -CMConfig.Loaded.MaxStabilizerRadius);
            stabilizeParticles.AddPos = new Vec3d(CMConfig.Loaded.MaxStabilizerRadius*2, CMConfig.Loaded.MaxStabilizerRadius*2, CMConfig.Loaded.MaxStabilizerRadius*2);
            stabilizeParticles.Color = ColorUtil.ReverseColorBytes(ColorUtil.HsvToRgba(h, 180, v));

            stabilizeParticles.MinSize = 0.2f;
            stabilizeParticles.ParticleModel = EnumParticleModel.Quad;
            stabilizeParticles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -150);
            stabilizeParticles.Color = ColorUtil.ReverseColorBytes(ColorUtil.HsvToRgba(h, 180, v, 150));

            Api.World.SpawnParticles(stabilizeParticles);

        }

        protected override void DoDeviceComplete()
        {
            deviceState = enDeviceState.RUNNING;
            tickCounter = 0;
        }

        protected override void DoFailedProcessing()
        {
            deviceState = enDeviceState.IDLE;
            tickCounter = 0;
        }

        public Vec3d Position => Pos.ToVec3d().Add(0.5);

        public string Type => "stabilizer";

        public bool Stabilize()
        {
            return isOn && IsPowered && DeviceState == enDeviceState.RUNNING;
        }

        private void InitAnim()
        {
            animUtil?.InitializeAnimator(Pos.ToString() + Block.Code);
        }
    }
}
