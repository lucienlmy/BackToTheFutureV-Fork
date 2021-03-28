﻿using BackToTheFutureV.TimeMachineClasses;
using FusionLibrary;
using FusionLibrary.Extensions;
using GTA;
using GTA.Math;
using GTA.Native;
using LemonUI.Menus;
using System;

namespace BackToTheFutureV.Menu
{
    internal class RCMenu : BTTFVMenu
    {
        private NativeListItem<TimeMachine> timeMachinesList;
        private NativeCheckboxItem FuelChamberDescription;
        private NativeCheckboxItem TimeCircuitsOnDescription;
        private NativeItem DestinationTimeDescription;

        private TimeMachine CurrentTimeMachine => timeMachinesList.SelectedItem;
        private Camera CarCam { get; set; }
        private bool CanBeSelected { get; set; }

        public RCMenu() : base("RC")
        {
            Shown += RCMenu_Shown;
            Closing += RCMenu_Closing;
            OnItemActivated += RCMenu_OnItemActivated;

            timeMachinesList = NewListItem<TimeMachine>("List");

            timeMachinesList.ItemChanged += TimeMachinesList_ItemChanged;

            FuelChamberDescription = NewCheckboxItem("Fuel");
            TimeCircuitsOnDescription = NewCheckboxItem("TC");
            DestinationTimeDescription = NewCheckboxItem("Destination");

            FuelChamberDescription.Enabled = false;
            TimeCircuitsOnDescription.Enabled = false;
            DestinationTimeDescription.Enabled = false;
        }

        private void RCMenu_Shown(object sender, EventArgs e)
        {
            timeMachinesList.Items = TimeMachineHandler.TimeMachines;

            CanBeSelected = TrySelectCar();
        }

        private void RCMenu_OnItemActivated(NativeItem sender, EventArgs e)
        {
            if (sender == timeMachinesList)
            {
                if (CanBeSelected)
                {
                    Close();

                    RemoteTimeMachineHandler.StartRemoteControl(CurrentTimeMachine);
                }
            }
        }

        private void RCMenu_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopPreviewing();
        }

        private void TimeMachinesList_ItemChanged(object sender, ItemChangedEventArgs<TimeMachine> e)
        {
            CanBeSelected = TrySelectCar();
        }

        private bool TrySelectCar()
        {
            FuelChamberDescription.Checked = CurrentTimeMachine.Properties.IsFueled;
            TimeCircuitsOnDescription.Checked = CurrentTimeMachine.Properties.AreTimeCircuitsOn;
            DestinationTimeDescription.Title = $"{GetItemTitle("Destination")} {CurrentTimeMachine.Properties.DestinationTime.ToString("MM/dd/yyyy hh:mm tt")}";

            if (Utils.PlayerPed.DistanceToSquared2D(CurrentTimeMachine, RemoteTimeMachineHandler.MAX_DIST) && CurrentTimeMachine.Vehicle.Driver == null)
            {
                PreviewCar();

                return true;
            }
            else
            {
                StopPreviewing();

                TextHandler.ShowNotification("UnableRC");
                return false;
            }
        }

        public void PreviewCar()
        {
            if (CarCam != null)
                CarCam?.Delete();

            CarCam = World.CreateCamera(CurrentTimeMachine.Vehicle.GetOffsetPosition(new Vector3(0.0f, -5.0f, 3.0f)), World.RenderingCamera.Rotation, 75.0f);
            CarCam.PointAt(CurrentTimeMachine.Vehicle);

            World.RenderingCamera = CarCam;

            Function.Call(Hash.CLEAR_FOCUS);
            Function.Call(Hash.SET_FOCUS_ENTITY, CurrentTimeMachine.Vehicle);
        }

        public void StopPreviewing()
        {
            Function.Call(Hash.CLEAR_FOCUS);
            CarCam?.Delete();
            CarCam = null;
            World.RenderingCamera = null;
        }

        public override void Tick()
        {
            if (Utils.PlayerVehicle != null)
                Close();
        }
    }
}
