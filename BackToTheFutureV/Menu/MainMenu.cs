﻿using BackToTheFutureV;
using FusionLibrary;
using FusionLibrary.Extensions;
using GTA;
using LemonUI.Menus;
using System;
using static BackToTheFutureV.InternalEnums;
using static FusionLibrary.Enums;

namespace BackToTheFutureV
{
    internal class MainMenu : BTTFVMenu
    {
        private NativeListItem<string> spawnBTTF;

        private NativeItem convertIntoTimeMachine;

        private NativeSubmenuItem rcMenu;
        private NativeSubmenuItem outatimeMenu;

        private NativeItem deleteCurrent;
        private NativeItem deleteOthers;
        private NativeItem deleteAll;

        public MainMenu() : base("Main")
        {
            Subtitle = TextHandler.GetLocalizedText("SelectOption");

            OnItemActivated += MainMenu_OnItemActivated;

            spawnBTTF = NewListItem("Spawn", TextHandler.GetLocalizedText("DMC12", "BTTF1", "BTTF1H", "BTTF2", "BTTF3", "BTTF3RR"));
            spawnBTTF.ItemChanged += SpawnBTTF_ItemChanged;
            spawnBTTF.Description = GetItemValueDescription("Spawn", "DMC12");

            NewSubmenu(MenuHandler.PresetsMenu, "Presets");

            convertIntoTimeMachine = NewItem("Convert");

            NewSubmenu(MenuHandler.CustomMenu, "Custom");

            rcMenu = NewSubmenu(MenuHandler.RCMenu, "RC");
            outatimeMenu = NewSubmenu(MenuHandler.OutatimeMenu, "Outatime");

            deleteCurrent = NewItem("Remove");
            deleteOthers = NewItem("RemoveOther");
            deleteAll = NewItem("RemoveAll");

            NewSubmenu(MenuHandler.SettingsMenu, "Settings");
        }

        private void SpawnBTTF_ItemChanged(object sender, ItemChangedEventArgs<string> e)
        {
            switch (e.Index)
            {
                case 0:
                    spawnBTTF.Description = GetItemValueDescription("Spawn", "DMC12");
                    break;
                case 1:
                case 2:
                    spawnBTTF.Description = GetItemValueDescription("Spawn", "BTTF1");
                    break;
                case 3:
                    spawnBTTF.Description = GetItemValueDescription("Spawn", "BTTF2");
                    break;
                case 4:
                    spawnBTTF.Description = GetItemValueDescription("Spawn", "BTTF3");
                    break;
                case 5:
                    spawnBTTF.Description = GetItemValueDescription("Spawn", "BTTF3RR");
                    break;
            }

            Recalculate();
        }

        public override void Tick()
        {
            convertIntoTimeMachine.Enabled = Utils.PlayerVehicle.IsFunctioning() && !Utils.PlayerVehicle.IsTimeMachine();

            outatimeMenu.Enabled = RemoteTimeMachineHandler.RemoteTimeMachineCount > 0;

            rcMenu.Enabled = Utils.PlayerVehicle == null && TimeMachineHandler.TimeMachineCount > 0;
        }

        private void MainMenu_OnItemActivated(NativeItem sender, EventArgs e)
        {
            TimeMachine timeMachine;

            if (sender == spawnBTTF)
            {
                if (spawnBTTF.SelectedIndex == 0)
                {
                    Utils.PlayerPed.Task.WarpIntoVehicle(DMC12Handler.CreateDMC12(Utils.PlayerPed.Position, Utils.PlayerPed.Heading), VehicleSeat.Driver);
                    Close();
                    return;
                }

                WormholeType wormholeType = WormholeType.BTTF1;

                switch (spawnBTTF.SelectedIndex)
                {
                    case 3:
                        wormholeType = WormholeType.BTTF2;
                        break;
                    case 4:
                    case 5:
                        wormholeType = WormholeType.BTTF3;
                        break;
                }

                if (ModSettings.CinematicSpawn)
                    timeMachine = TimeMachineHandler.Create(SpawnFlags.ForceReentry | SpawnFlags.New, wormholeType);
                else
                    timeMachine = TimeMachineHandler.Create(SpawnFlags.WarpPlayer | SpawnFlags.New, wormholeType);

                if (spawnBTTF.SelectedIndex == 2)
                {
                    timeMachine.Mods.Hook = HookState.OnDoor;
                    timeMachine.Mods.Plate = PlateType.Empty;

                    timeMachine.Properties.ReactorCharge = 0;
                }

                if (spawnBTTF.SelectedIndex == 5)
                    timeMachine.Mods.Wheel = WheelType.RailroadInvisible;
            }

            if (sender == convertIntoTimeMachine)
                Utils.PlayerVehicle.TransformIntoTimeMachine();

            if (sender == deleteCurrent)
            {
                timeMachine = TimeMachineHandler.GetTimeMachineFromVehicle(Utils.PlayerVehicle);

                if (timeMachine == null)
                {
                    TextHandler.ShowNotification("NotSeated");
                    return;
                }

                TimeMachineHandler.RemoveTimeMachine(timeMachine);

                ExternalHUD.SetOff();
            }

            if (sender == deleteOthers)
            {
                TimeMachineHandler.RemoveAllTimeMachines(true);
                RemoteTimeMachineHandler.DeleteAll();
                WaybackMachineHandler.Abort();
                TextHandler.ShowNotification("RemovedOtherTimeMachines");
            }

            if (sender == deleteAll)
            {
                TimeMachineHandler.RemoveAllTimeMachines();
                RemoteTimeMachineHandler.DeleteAll();
                WaybackMachineHandler.Abort();
                TextHandler.ShowNotification("RemovedAllTimeMachines");

                ExternalHUD.SetOff();
            }

            Close();
        }
    }
}
