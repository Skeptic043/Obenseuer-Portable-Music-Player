using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenseuerCore;
using PortableMusicPlayer.Core;
using PortableMusicPlayer.Items;
using PortableMusicPlayer.Library;
using PortableMusicPlayer.Ui;
using UnityEngine;

namespace PortableMusicPlayer
{
    internal static class GamePatches
    {
        private static readonly MethodInfo ChargerOff = AccessTools.Method(typeof(BatteryPowerSource), "PowerOFF");
        private static readonly MethodInfo InputAxis = AccessTools.Method(typeof(Input), nameof(Input.GetAxis), new[] { typeof(string) });
        private static readonly MethodInfo FilteredAxis = AccessTools.Method(typeof(GamePatches), nameof(ViewAxis));
        private static Harmony viewPatches;
        private static FieldInfo keyCaptureActive, keyCaptureStart, keyCaptureBinding;
        private static MethodInfo keyCaptureEnd, keyBindingBind;
        private static int cancelledFrame = -1;

        internal static void Apply(CoreMod mod, CoreLog log)
        {
            if (!mod.Postfix(typeof(BatteryPowerSource), nameof(BatteryPowerSource.CheckItems), typeof(GamePatches), nameof(RefusePocketPlayerCharging)))
                log.Warn("Chargers could not be stopped from charging the Pocket Music Player.");
            if (!mod.Postfix(typeof(HeadSlotController), nameof(HeadSlotController.OnDropped),
                    new[] { typeof(Item), typeof(int), typeof(int), typeof(object[]) }, typeof(GamePatches), nameof(MuffleForWorkHeadphones)))
                log.Warn("Work Headphones will not muffle the world.");
            if (!mod.Postfix(typeof(ToolTip), nameof(ToolTip.TextDataToString), new[] { typeof(OS.Items.ItemTextData) },
                    typeof(GamePatches), nameof(HidePlayerIdentity)))
                log.Warn("Portable player identity may be visible in item details.");
            if (!mod.Postfix(typeof(Radio), "Awake", typeof(GamePatches), nameof(NoteRadio)))
                log.Warn("Game Radio may miss radios that load after start-up.");
            ApplyViewPatch(typeof(ECM.Components.MouseLook), nameof(ECM.Components.MouseLook.LookRotation), log);
            ApplyViewPatch(typeof(ThirdPersonCameraController), "LateUpdate", log);
            ApplyKeyCapturePatch(log);
            if (!mod.Prefix(typeof(GameController), "Update", typeof(GamePatches), nameof(ClosePlayerBeforePause)))
                log.Warn("Escape may open the pause menu before closing the music player.");
            if (!mod.Prefix(typeof(PauseMenu), "Update", typeof(GamePatches), nameof(ClosePlayerBeforePause)))
                log.Warn("The pause menu may open before Escape closes the music player.");
        }

        internal static void Remove()
        {
            viewPatches?.UnpatchSelf();
            viewPatches = null;
            cancelledFrame = -1;
        }

        private static void ApplyViewPatch(System.Type type, string methodName, CoreLog log)
        {
            try
            {
                viewPatches = viewPatches ?? new Harmony(ModInfo.Guid + ".viewinput");
                MethodInfo original = AccessTools.Method(type, methodName);
                if (original == null) throw new System.MissingMethodException(type.FullName, methodName);
                viewPatches.Patch(original, transpiler: new HarmonyMethod(
                    AccessTools.Method(typeof(GamePatches), nameof(FilterViewAxes))));
            }
            catch (System.Exception error)
            {
                log.Warn(type.Name + " view input could not be filtered: " + error.Message);
            }
        }

        // OMF's binding control stores mouse buttons correctly, but its capture loop deliberately skips
        // every Unity mouse KeyCode. Handle those codes before the framework loop and let it keep owning
        // the binding, display, config file and keyboard capture behavior.
        private static void ApplyKeyCapturePatch(CoreLog log)
        {
            try
            {
                System.Type capture = AccessTools.TypeByName("ObenseuerCore.Ui.KeyCapture");
                System.Type binding = AccessTools.TypeByName("ObenseuerCore.Ui.ModKeyBinding");
                if (capture == null || binding == null) throw new System.TypeLoadException("OMF keybind capture types were not found");
                keyCaptureActive = AccessTools.Field(capture, "capturing");
                keyCaptureStart = AccessTools.Field(capture, "startFrame");
                keyCaptureBinding = AccessTools.Field(capture, "Binding");
                keyCaptureEnd = AccessTools.Method(capture, "End");
                keyBindingBind = AccessTools.Method(binding, "Bind", new[] { typeof(KeyCode) });
                MethodInfo update = AccessTools.Method(capture, "Update");
                if (keyCaptureActive == null || keyCaptureStart == null || keyCaptureBinding == null ||
                    keyCaptureEnd == null || keyBindingBind == null || update == null)
                    throw new System.MissingMemberException("OMF keybind capture members were not found");
                viewPatches = viewPatches ?? new Harmony(ModInfo.Guid + ".input");
                viewPatches.Patch(update, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(GamePatches), nameof(CaptureMouseButton))));
            }
            catch (System.Exception error)
            {
                log.Warn("Mouse buttons could not be enabled in the framework keybind manager: " + error.Message);
            }
        }

        private static bool CaptureMouseButton(object __instance)
        {
            if (__instance == null || keyCaptureActive == null || !(bool)keyCaptureActive.GetValue(__instance) ||
                Time.frameCount == (int)keyCaptureStart.GetValue(__instance)) return true;
            for (int value = (int)KeyCode.Mouse0; value <= (int)KeyCode.Mouse6; value++)
            {
                KeyCode code = (KeyCode)value;
                if (!Input.GetKeyDown(code)) continue;
                object binding = keyCaptureBinding.GetValue(__instance);
                keyCaptureEnd.Invoke(__instance, null);
                if (binding != null) keyBindingBind.Invoke(binding, new object[] { code });
                return false;
            }
            return true;
        }

        // A charger is a battery power source with a negative drain. It picks every stored stack with charge
        // data, so the AA-powered player is taken back out. Battery-powered devices are left alone.
        private static void RefusePocketPlayerCharging(BatteryPowerSource __instance, List<ItemStack> ___fuelStacks)
        {
            if (__instance.fuelDrainPerHour >= 0f || ___fuelStacks == null) return;
            int removed = ___fuelStacks.RemoveAll(stack => stack?.itemReference != null &&
                ItemSetup.TryDevice(stack.itemReference.ID, out Device device) && !Catalog.Entry(device).Charges &&
                Catalog.Entry(device).IsPlayer);
            // The game already switched the charger on for what it found.
            if (removed > 0 && ___fuelStacks.Count == 0) ChargerOff?.Invoke(__instance, null);
        }

        private static void NoteRadio() => GameRadioLibrary.RadioAppeared = true;

        // The game muffles the world for Earmuffs by item ID in this method, and unmuffles on any removal,
        // load or underwater change. Work Headphones join that native path.
        private static void MuffleForWorkHeadphones(Item ___currentItem)
        {
            if (___currentItem == null || !ItemSetup.TryDevice(___currentItem, out Device device) || Catalog.Entry(device).IsPlayer) return;
            if (OutputProfile.For(device).MufflesWorld) UnderwaterEffect.instance?.Muffle();
        }

        private static void HidePlayerIdentity(OS.Items.ItemTextData __0, ref string __result)
        {
            if (__0 != null && PortablePlayerIdentity.IsToken(__0.content)) __result = PortablePlayerIdentity.TooltipText;
        }

        // Keep camera follow and positioning alive; only suppress view-axis deltas while the overlay
        // owns the cursor. Skipping the whole third-person update makes movement catch up on close.
        private static IEnumerable<CodeInstruction> FilterViewAxes(IEnumerable<CodeInstruction> instructions)
        {
            var filtered = new List<CodeInstruction>();
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(InputAxis)) instruction.operand = FilteredAxis;
                filtered.Add(instruction);
            }
            return filtered;
        }

        private static float ViewAxis(string axis) => PlayerWindow.BlocksViewRotation ? 0f : Input.GetAxis(axis);

        private static bool ClosePlayerBeforePause()
        {
            if (Time.frameCount == cancelledFrame) return false;
            if (!PlayerWindow.IsAnyOpen || InputManager.instance == null) return true;
            if (!InputManager.instance.GetKeyDown("Pause Menu") && !InputManager.instance.GetKeyDown("Menu")) return true;
            cancelledFrame = Time.frameCount;
            PlayerWindow.TryCloseFromCancel();
            return false;
        }
    }
}
