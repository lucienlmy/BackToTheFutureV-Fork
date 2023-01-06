using FusionLibrary;
using FusionLibrary.Extensions;
using FusionLibrary.Memory;
using GTA;
using MinHook;
using System;
using System.Runtime.InteropServices;
using static BackToTheFutureV.InternalEnums;
using static FusionLibrary.FusionEnums;

namespace BackToTheFutureV
{
    [StructLayout(LayoutKind.Explicit)]
    public struct Vec3V
    {
        [FieldOffset(0)] public float X;
        [FieldOffset(4)] public float Y;
        [FieldOffset(8)] public float Z;
        [FieldOffset(12)] public float W;

        public Vec3V(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;

            W = x;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct CSpecialFlightHandlingData
    {
        [FieldOffset(16)] public Vec3V vecAngularDamping;
        [FieldOffset(32)] public Vec3V vecAngularDampingMin;
        [FieldOffset(48)] public Vec3V vecLinearDamping;
        [FieldOffset(64)] public Vec3V vecLinearDampingMin;
        [FieldOffset(80)] public float fLiftCoefficient;
        [FieldOffset(84)] public float fCriticalLiftAngle;
        [FieldOffset(88)] public float fInitialLiftAngle;
        [FieldOffset(92)] public float fMaxLiftAngle;
        [FieldOffset(96)] public float fDragCoefficient;
        [FieldOffset(100)] public float fBrakingDrag;
        [FieldOffset(104)] public float fMaxLiftVelocity;
        [FieldOffset(108)] public float fMinLiftVelocity;
        [FieldOffset(112)] public float fRollTorqueScale;
        [FieldOffset(116)] public float fMaxTorqueVelocity;
        [FieldOffset(120)] public float fMinTorqueVelocity;
        [FieldOffset(124)] public float fYawTorqueScale;
        [FieldOffset(128)] public float fSelfLevelingPitchTorqueScale;
        [FieldOffset(132)] public float fInitalOverheadAssist;
        [FieldOffset(136)] public float fMaxPitchTorque;
        [FieldOffset(140)] public float fMaxSteeringRollTorque;
        [FieldOffset(144)] public float fPitchTorqueScale;
        [FieldOffset(148)] public float fSteeringTorqueScale;
        [FieldOffset(152)] public float fMaxThrust;
        [FieldOffset(156)] public float fTransitionDuration;
        [FieldOffset(160)] public float fHoverVelocityScale;
        [FieldOffset(164)] public float fStabilityAssist;
        [FieldOffset(168)] public float fMinSpeedForThrustFalloff;
        [FieldOffset(172)] public float fBrakingThrustScale;
        [FieldOffset(176)] public int mode;
        [FieldOffset(180)] public long strFlags1;
        [FieldOffset(188)] public long strFlags2;
    }

    public class HoverMode : Script
    {
        private readonly HookEngine _hook = new HookEngine();
        private CSpecialFlightHandlingData _customFlightData = new CSpecialFlightHandlingData();
        private GCHandle _customFlightData_Handle;
        private IntPtr pSubHandling;
        private bool _firstTick = true;
        private NativeInput _flyModeInput;
        private int _nextModeChangeAllowed;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        delegate IntPtr CHandling_GetSubHandlingByType_Delegate(IntPtr inst, int type);
        CHandling_GetSubHandlingByType_Delegate CHandling_GetSubHandlingByType_Original;
        unsafe IntPtr CHandling_GetSubHandlingByType_Detour(IntPtr inst, int type)
        {
            if (type == 10) // rage::par::SUB_HANDLING_SPECIAL_FLIGHT
            {
                return pSubHandling;
            }

            return CHandling_GetSubHandlingByType_Original(inst, type);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        unsafe delegate void DeluxoSubHandling_UpdateAnimationBones1_Delegate(long pVehicle, uint a2, float a3, float* a4, float* a5, float a6);
        unsafe void DeluxoSubHandling_UpdateAnimationBones1_Detour(long pVehicle, uint a2, float a3, float* a4, float* a5, float a6)
        {

        }

        public HoverMode()
        {
            Tick += HoverMode_Tick;
            Aborted += HoverMode_Aborted;
            //KeyDown += HoverMode_KeyDown;
        }

        private void HoverMode_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == System.Windows.Forms.Keys.Q && FusionUtils.PlayerVehicle.NotNullAndExists())
            {
                FusionUtils.PlayerVehicle.SetHoverModeAllowed(!FusionUtils.PlayerVehicle.IsHoverModeAllowed());
            }

            if (e.KeyCode == System.Windows.Forms.Keys.L)
            {
                foreach (Vehicle vehicle in World.GetAllVehicles())
                {
                    if (vehicle.IsBoat)
                        continue;

                    vehicle.SetHoverModeAllowed(true);
                    vehicle.SetHoverMode(true);
                }
            }
        }

        private void CreateSubHandling()
        {
            // From handling.meta
            _customFlightData.mode = 1;
            _customFlightData.fLiftCoefficient = 50.0f;
            _customFlightData.fMinLiftVelocity = 0.0f;
            _customFlightData.fDragCoefficient = 0.0f;
            _customFlightData.fMaxPitchTorque = 500.0f;
            _customFlightData.fMaxSteeringRollTorque = 50.0f;
            _customFlightData.fMaxThrust = 20.0f;
            _customFlightData.fYawTorqueScale = -4.0f;
            _customFlightData.fRollTorqueScale = 7.5f;
            _customFlightData.fTransitionDuration = 1.0f;
            _customFlightData.fPitchTorqueScale = 8.0f;
            _customFlightData.vecAngularDamping = new Vec3V(3.0f, 2.0f, 1.2f);
            _customFlightData.vecLinearDamping = new Vec3V(0.9f, 0.1f, 0.7f);
            _customFlightData.vecAngularDampingMin = new Vec3V(0, 0, 0);
            _customFlightData.vecLinearDampingMin = new Vec3V(0, 0, 0);

            // From in game (defaults)
            _customFlightData.fCriticalLiftAngle = 45.0f;
            _customFlightData.fInitialLiftAngle = 1.5f;
            _customFlightData.fMaxLiftAngle = 25.0f;
            _customFlightData.fBrakingDrag = 10.0f;
            _customFlightData.fMaxLiftVelocity = 2000.0f;
            _customFlightData.fRollTorqueScale = 7.5f;
            _customFlightData.fMaxTorqueVelocity = 100.0f;
            _customFlightData.fMinTorqueVelocity = 40000.0f;
            _customFlightData.fYawTorqueScale = -4.0f;
            _customFlightData.fSelfLevelingPitchTorqueScale = -5.0f;
            _customFlightData.fInitalOverheadAssist = -5.0f;
            _customFlightData.fSteeringTorqueScale = 1000.0f;
            _customFlightData.fMaxThrust = 20.0f;
            _customFlightData.fHoverVelocityScale = 1.0f;
            _customFlightData.fStabilityAssist = 10.0f;
            _customFlightData.fMinSpeedForThrustFalloff = 0.0f;
            _customFlightData.fBrakingThrustScale = 0.0f;
        }

        private void HoverMode_Tick(object sender, EventArgs e)
        {
            if (!_firstTick)
            {
                foreach (Vehicle vehicle in World.GetAllVehicles())
                {
                    if (!vehicle.NotNullAndExists())
                        continue;

                    if (!vehicle.Decorator().Exists(BTTFVDecors.AllowHoverMode))
                        vehicle.SetHoverModeAllowed(false);
                }
            }

            if (Game.IsLoading || !_firstTick)
                return;

            Decorator.Register(BTTFVDecors.AllowHoverMode, DecorType.Bool);
            Decorator.Lock();

            CreateSubHandling();

            _customFlightData_Handle = GCHandle.Alloc(_customFlightData, GCHandleType.Pinned);
            pSubHandling = _customFlightData_Handle.AddrOfPinnedObject();

            unsafe
            {
                // DeluxoSubHandling::UpdateBoneAnimations1
                IntPtr addr = Game.FindPattern("E8 ? ? ? ? EB 30 49 63 0F");
                addr += 1;
                addr = addr + 4 + *(int*)addr;

                _hook.CreateHook(addr, new DeluxoSubHandling_UpdateAnimationBones1_Delegate(DeluxoSubHandling_UpdateAnimationBones1_Detour));

                // CHandling::GetSubHandlingByType
                addr = Game.FindPattern("E8 ?? ?? ?? ?? 66 3B 70 48");
                addr += 1;
                addr = addr + 4 + *(int*)addr;

                CHandling_GetSubHandlingByType_Original = _hook.CreateHook(addr, new CHandling_GetSubHandlingByType_Delegate(CHandling_GetSubHandlingByType_Detour));
            }

            _hook.EnableHooks();
            _firstTick = false;

            _flyModeInput = new NativeInput(ModControls.Hover);
            _flyModeInput.OnControlLongPressed += OnFlyModeControlJustLongPressed;
            _flyModeInput.OnControlPressed += OnFlyModeControlJustPressed;
        }

        private void HoverMode_Aborted(object sender, EventArgs e)
        {
            _hook.DisableHooks();
            _customFlightData_Handle.Free();
        }

        private void OnFlyModeControlJustLongPressed()
        {
            if (ModControls.LongPressForHover)
                SwitchHover(FusionUtils.PlayerVehicle);
        }

        private void OnFlyModeControlJustPressed()
        {
            if (!ModControls.LongPressForHover)
                SwitchHover(FusionUtils.PlayerVehicle);
        }

        private void SwitchHover(Vehicle vehicle)
        {
            if (_nextModeChangeAllowed > Game.GameTime || !vehicle.NotNullAndExists() || !vehicle.IsHoverModeAllowed() || vehicle.Model == ModelHandler.DMC12)
                return;

            vehicle.SetHoverMode(!vehicle.IsInHoverMode());

            _nextModeChangeAllowed = Game.GameTime + 2000;
        }
    }
}