﻿using BackToTheFutureV.TimeMachineClasses.Handlers;
using BackToTheFutureV.TimeMachineClasses.RC;
using BackToTheFutureV.Utility;
using BackToTheFutureV.Vehicles;
using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BackToTheFutureV.TimeMachineClasses
{
    public class TimeMachineHandler
    {
        public static TimeMachine LastTimeMachine { get; private set; }
        public static TimeMachine ClosestTimeMachine { get; private set; }
        public static TimeMachine CurrentTimeMachine { get; private set; }
        public static float SquareDistToClosestTimeMachine { get; private set; } = -1;

        private static List<TimeMachine> _timeMachines = new List<TimeMachine>();
        private static List<TimeMachine> _timeMachinesToAdd = new List<TimeMachine>();
        private static Dictionary<TimeMachine, bool> _timeMachinesToRemove = new Dictionary<TimeMachine, bool>();
        private static Dictionary<TimeMachine, bool> _timeMachinesToRemoveWaitSounds = new Dictionary<TimeMachine, bool>();

        public static int TimeMachineCount => _timeMachines.Count;
        private static bool _savedEmpty;

        public static void SaveAllTimeMachines()
        {
            if (TimeMachineCount == 0 && _savedEmpty)
                return;

            TimeMachineCloneManager.Save(_timeMachines);

            _savedEmpty = TimeMachineCount == 0;
        }

        public static void LoadAllTimeMachines()
        {
            try
            {
                TimeMachineCloneManager.Load()?.SpawnAll();
            }
            catch
            {
                TimeMachineCloneManager.Delete();
            }
        }

        public static TimeMachine CreateTimeMachine(DMC12 dmc12, WormholeType wormholeType)
        {
            return new TimeMachine(dmc12, wormholeType);
        }

        public static TimeMachine CreateTimeMachine(Vehicle vehicle, WormholeType wormholeType)
        {
            if (vehicle == null)
                return null;

            if (vehicle.Model.IsTrain)
                return null;

            return new TimeMachine(vehicle, wormholeType);
        }

        public static TimeMachine CreateTimeMachine(Vector3 position, float heading = 0, WormholeType wormholeType = WormholeType.BTTF1)
        {
            return new TimeMachine(DMC12Handler.CreateDMC12(position, heading), wormholeType);
        }

        public static void AddTimeMachine(TimeMachine vehicle)
        {
            if (_timeMachinesToAdd.Contains(vehicle) || _timeMachines.Contains(vehicle))
                return;

            _timeMachinesToAdd.Add(vehicle);
        }

        public static void RemoveTimeMachine(TimeMachine vehicle, bool deleteVeh = true, bool waitSoundsComplete = false)
        {
            if (_timeMachinesToRemove.ContainsKey(vehicle))
                return;

            if (waitSoundsComplete)
            {
                if (!_timeMachinesToRemoveWaitSounds.ContainsKey(vehicle))
                    _timeMachinesToRemoveWaitSounds.Add(vehicle, deleteVeh);
            }
            else
                _timeMachinesToRemove.Add(vehicle, deleteVeh);
        }

        public static void RemoveInstantlyTimeMachine(TimeMachine vehicle, bool deleteVeh = true)
        {
            if (_timeMachinesToRemoveWaitSounds.ContainsKey(vehicle))
                _timeMachinesToRemoveWaitSounds.Remove(vehicle);
            
            vehicle?.Dispose(deleteVeh);

            _timeMachines.Remove(vehicle);
        }

        public static void RemoveAllTimeMachines(bool noCurrent = false)
        {
            foreach (var veh in _timeMachines.ToList())
            {
                if (noCurrent && veh.Vehicle == Main.PlayerVehicle)
                    continue;

                RemoveTimeMachine(veh);
            }
        }

        public static TimeMachine SpawnWithReentry(WormholeType wormholeType = WormholeType.BTTF1, string presetName = default)
        {
            if (wormholeType == WormholeType.DMC12)
                return Spawn(wormholeType);

            TimeMachineClone timeMachineClone = null;

            if (presetName != default)
                timeMachineClone = TimeMachineClone.Load(presetName);

            TimeMachine timeMachine;

            if (timeMachineClone != null)
            {
                timeMachineClone.Vehicle.Position = Main.PlayerPed.GetOffsetPosition(new Vector3(0, 25, 0));
                timeMachineClone.Vehicle.Heading = Main.PlayerPed.Heading + 180;

                timeMachine = timeMachineClone.Spawn(true, true);
            }                
            else
            {
                timeMachine = CreateTimeMachine(Main.PlayerPed.GetOffsetPosition(new Vector3(0, 25, 0)), Main.PlayerPed.Heading + 180, wormholeType);

                Utils.HideVehicle(timeMachine.Vehicle, true);

                timeMachine.Properties.DestinationTime = Main.CurrentTime;

                timeMachine.Properties.AreTimeCircuitsOn = true;
                timeMachine.Events.SetTimeCircuits?.Invoke(true);

                timeMachine.Events.OnReenter?.Invoke();
            }
                
            return timeMachine;
        }

        public static TimeMachine Spawn(WormholeType wormholeType)
        {
            Ped ped = Main.PlayerPed;

            if (RCManager.RemoteControlling != null)
            {
                ped = RCManager.RemoteControlling.OriginalPed;
                RCManager.StopRemoteControl(true);
            }

            Vector3 spawnPos;

            if (Main.PlayerVehicle != null)
                spawnPos = Main.PlayerVehicle.Position.Around(5f);
            else
                spawnPos = ped.Position;

            TimeMachine timeMachine = CreateTimeMachine(spawnPos, ped.Heading, wormholeType);

            ped.SetIntoVehicle(timeMachine.Vehicle, VehicleSeat.Driver);

            timeMachine.Vehicle.PlaceOnGround();

            timeMachine.Vehicle.SetMPHSpeed(1);

            return timeMachine;
        }

        public static void KeyDown(Keys e)
        {
            _timeMachines.ForEach(x => x.KeyDown(e));
        }

        public static void Process()
        {
            if (_timeMachinesToRemoveWaitSounds.Count > 0)
            {
                foreach (var timeMachine in _timeMachinesToRemoveWaitSounds)
                    if (!timeMachine.Key.Sounds.AudioEngine.IsAnyInstancePlaying)
                        RemoveTimeMachine(timeMachine.Key, timeMachine.Value);
            }

            if (_timeMachinesToRemove.Count > 0)
            {
                foreach (var timeMachine in _timeMachinesToRemove)
                    RemoveInstantlyTimeMachine(timeMachine.Key, timeMachine.Value);

                _timeMachinesToRemove.Clear();
            }

            if (_timeMachinesToAdd.Count > 0)
            {
                _timeMachines.AddRange(_timeMachinesToAdd);
                _timeMachinesToAdd.Clear();
            }

            UpdateClosestTimeMachine();

            foreach (var timeMachine in _timeMachines)
            {
                if (timeMachine.Disposed || !timeMachine.Vehicle.Exists())
                {
                    RemoveTimeMachine(timeMachine);
                    continue;
                }

                timeMachine.Process();
            }                
        }

        public static void Abort()
        {
            _timeMachines.ForEach(x => x.Dispose(false));
        }

        public static TimeMachine GetTimeMachineFromIndex(int index)
        {
            if (index > TimeMachineCount - 1)
                return default;

            return _timeMachines[index];
        }

        public static TimeMachine GetTimeMachineFromVehicle(Vehicle vehicle)
        {
            if (vehicle == null)
                return null;

            foreach (var timeMachine in _timeMachines)
            {
                if (timeMachine.Vehicle == vehicle)
                    return timeMachine;
            }

            foreach (var timeMachine in _timeMachinesToAdd)
            {
                if (timeMachine.Vehicle == vehicle)
                    return timeMachine;
            }

            return null;
        }

        public static bool IsVehicleATimeMachine(Vehicle vehicle)
        {
            foreach (var timeMachine in _timeMachines)
                if (timeMachine.Vehicle == vehicle)
                    return true;

            foreach (var timeMachine in _timeMachinesToAdd)
                if (timeMachine.Vehicle == vehicle)
                    return true;

            return false;
        }

        public static void ExistenceCheck(DateTime time)
        {
            _timeMachines.ForEach(x =>
            {
                if (x.LastDisplacementClone.Properties.DestinationTime > time && Main.PlayerVehicle != x.Vehicle)
                    RemoveTimeMachine(x);
            });
        }

        public static void UpdateClosestTimeMachine()
        {
            if (Main.PlayerVehicle != null && CurrentTimeMachine != null && CurrentTimeMachine.Vehicle == Main.PlayerVehicle)
                return;

            CurrentTimeMachine = null;

            if (_timeMachines.Count == 0 && SquareDistToClosestTimeMachine != -1)
            {
                ClosestTimeMachine = null;
                SquareDistToClosestTimeMachine = -1;
            }
                            
            foreach (var timeMachine in _timeMachines)
            {                
                float dist = timeMachine.Vehicle.Position.DistanceToSquared(Main.PlayerPed.Position);

                if (ClosestTimeMachine == timeMachine)
                    SquareDistToClosestTimeMachine = dist;

                if (ClosestTimeMachine != timeMachine && (SquareDistToClosestTimeMachine == -1 || dist < SquareDistToClosestTimeMachine))
                {
                    if (ClosestTimeMachine != null)
                        ClosestTimeMachine.Properties.IsGivenScaleformPriority = false;

                    ClosestTimeMachine = timeMachine;

                    ClosestTimeMachine.Properties.IsGivenScaleformPriority = true;
                    ClosestTimeMachine.Events.OnScaleformPriority?.Invoke();

                    SquareDistToClosestTimeMachine = dist;               
                }
            }

            if (ClosestTimeMachine != null && Main.PlayerVehicle == ClosestTimeMachine.Vehicle)
                CurrentTimeMachine = ClosestTimeMachine;
        }
    }
}
