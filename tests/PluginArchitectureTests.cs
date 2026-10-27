using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace PortableMusicPlayer.Tests
{
    [TestSuite]
    public static class PluginArchitectureTests
    {
        public static void ThePluginRegistersDuringBepInExAwakeWithoutAVersionPin()
        {
            CheckPlugin("BepInEx5", "PortableMusicPlayer.BepInEx5.dll", "BepInEx.BaseUnityPlugin",
                Path.Combine(Program.GamePath, "BepInEx", "core"));
            CheckPlugin("BepInEx6", "PortableMusicPlayer.dll", "BepInEx.Unity.Mono.BaseUnityPlugin",
                Program.BepInEx6CorePath);
        }

        public static void ARepairedActiveIdentityIsWrittenBackToSavedState()
        {
            CheckIdentityWriteback("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckIdentityWriteback("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void CatalogueOwnershipIsVerifiedBeforeItemsAreNormalizedOrStartupCommits()
        {
            CheckStartupFlow("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckStartupFlow("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void CatalogueRebuildsReverifyBeforeRenormalizingItems()
        {
            CheckCatalogueReload("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckCatalogueReload("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void WorldModelsLieFlatAndUseABottomPivot()
        {
            CheckWorldModelPresentation("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckWorldModelPresentation("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void PrivateAudioBypassesEnvironmentalReverb()
        {
            CheckEnvironmentalReverb("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckEnvironmentalReverb("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void PlaybackKeysUseTheFrameworkRegistryAndDispatchOnlyFromThePoweredKeyHandler()
        {
            CheckPlaybackKeys("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckPlaybackKeys("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void FrameworkKeybindCaptureAcceptsMouseButtons()
        {
            CheckMouseKeybindCapture("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckMouseKeybindCapture("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void PluginConfigIsBoundDuringAwakeBeforeTheFrameworkBuildsItsMenu()
        {
            CheckEarlyConfigBinding("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckEarlyConfigBinding("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void ThePlayerIsANonModalOverlay()
        {
            CheckPlayerOverlay("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckPlayerOverlay("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void TheOverlayFreezesViewRotationWithoutTakingMovementControls()
        {
            CheckViewInputPatch("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckViewInputPatch("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void EscapeClosesThePlayerBeforeThePauseMenuOpens()
        {
            CheckEscapeHandling("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckEscapeHandling("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void TheDefaultGameRadioIsAPlayableCollection()
        {
            CheckGameRadioCollection("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckGameRadioCollection("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void ThePocketPlayerAlwaysHasAChargeAwareBatteryReplacementAction()
        {
            CheckBatteryReplacement("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckBatteryReplacement("BepInEx6", "PortableMusicPlayer.dll");
        }

        public static void PlayerNotificationsUseTheRequestedShortSentences()
        {
            CheckPlayerMessages("BepInEx5", "PortableMusicPlayer.BepInEx5.dll");
            CheckPlayerMessages("BepInEx6", "PortableMusicPlayer.dll");
        }

        private static void CheckPlaybackKeys(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition settings = assembly.MainModule.GetType("PortableMusicPlayer.Settings");
                MethodDefinition constructor = settings?.Methods.SingleOrDefault(method => method.IsConstructor && !method.IsStatic);
                var registration = constructor?.Body.Instructions.ToList();
                var defaults = new[]
                {
                    91, // UnityEngine.KeyCode.LeftBracket
                    92, // UnityEngine.KeyCode.Backslash
                    93  // UnityEngine.KeyCode.RightBracket
                };
                Assert.Equal(defaults.Length, defaults.Distinct().Count(), loader + " playback-key defaults are not distinct");
                CheckFrameworkKey(settings, registration, "TogglePower", 0, loader);
                CheckFrameworkKey(settings, registration, "OpenPlayer", 0, loader);
                CheckFrameworkKey(settings, registration, "PreviousTrack", defaults[0], loader);
                CheckFrameworkKey(settings, registration, "PlayPause", defaults[1], loader);
                CheckFrameworkKey(settings, registration, "NextTrack", defaults[2], loader);

                TypeDefinition player = assembly.MainModule.GetType("PortableMusicPlayer.Playback.MusicPlayer");
                MethodDefinition tick = player?.Methods.SingleOrDefault(method => method.Name == "Tick");
                MethodDefinition keys = player?.Methods.SingleOrDefault(method => method.Name == "HandleKeys");
                Assert.True(tick != null && References(tick, "HandleKeys"), loader + " tick does not use the shared key handler");
                Assert.True(keys != null && References(keys, "get_IsTyping"), loader + " key handler is not blocked while typing");
                var keyInstructions = keys?.Body.Instructions.ToList();
                int powerAt = keyInstructions?.FindIndex(instruction =>
                    instruction.Operand is FieldReference field && field.DeclaringType.FullName == "PortableMusicPlayer.Core.SavedState" &&
                    field.Name == "Powered") ?? -1;
                int firstPlaybackKey = keyInstructions?.FindIndex(instruction =>
                    instruction.Operand is MethodReference method && new[]
                        { "get_PreviousTrack", "get_PlayPause", "get_NextTrack" }.Contains(method.Name)) ?? -1;
                Assert.True(powerAt >= 0 && firstPlaybackKey > powerAt && keyInstructions.Skip(powerAt + 1)
                        .Take(firstPlaybackKey - powerAt - 1).Any(instruction =>
                            instruction.OpCode.FlowControl == Mono.Cecil.Cil.FlowControl.Cond_Branch),
                    loader + " playback keys are not behind the player-power guard");
                foreach (string property in new[] { "get_PreviousTrack", "get_PlayPause", "get_NextTrack" })
                    Assert.True(References(keys, property), loader + " key handler does not read " + property.Substring(4));
                foreach (string action in new[] { "Previous", "TogglePause", "Next" })
                    Assert.True(References(keys, action), loader + " key handler does not dispatch " + action);
                Assert.True(keys.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method &&
                        method.DeclaringType.FullName == "ObenseuerCore.ModKeybind" && method.Name == "Down"),
                    loader + " key handler does not use the selected framework binding source");
            }
        }

        private static void CheckEarlyConfigBinding(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition plugin = assembly.MainModule.GetType("PortableMusicPlayer.PortableMusicPlayerMod");
                MethodDefinition awake = plugin?.Methods.SingleOrDefault(method => method.Name == "Awake");
                MethodDefinition finish = plugin?.Methods.SingleOrDefault(method => method.Name == "FinishLoad");
                Assert.True(awake != null && awake.Body.Instructions.Any(instruction =>
                        instruction.OpCode.Code == Mono.Cecil.Cil.Code.Newobj && instruction.Operand is MethodReference created &&
                        created.DeclaringType.FullName == "PortableMusicPlayer.Settings") &&
                    finish != null && !finish.Body.Instructions.Any(instruction =>
                        instruction.OpCode.Code == Mono.Cecil.Cil.Code.Newobj && instruction.Operand is MethodReference created &&
                        created.DeclaringType.FullName == "PortableMusicPlayer.Settings"),
                    loader + " binds its config after the framework can build the Mods menu");
            }
        }

        private static void CheckPlayerOverlay(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition overlay = assembly.MainModule.GetType("PortableMusicPlayer.Ui.PlayerWindow");
                Assert.True(overlay != null, loader + " player overlay is missing");
                Assert.True(!overlay.Fields.Any(field => field.FieldType.FullName == "ObenseuerCore.Ui.ModWindow") &&
                        !overlay.Methods.SelectMany(method => method.HasBody ? method.Body.Instructions : Enumerable.Empty<Mono.Cecil.Cil.Instruction>())
                            .Any(instruction => instruction.Operand is MethodReference method &&
                                method.DeclaringType.FullName == "ObenseuerCore.Ui.ModWindow"),
                    loader + " player still uses the modal framework window");
                Assert.True(!overlay.Methods.Any(method => method.HasBody && method.Body.Instructions.Any(instruction =>
                        instruction.Operand is MethodReference called && called.Name == "ControlsDisabled")),
                    loader + " overlay still disables gameplay controls");
            }
        }

        private static void CheckMouseKeybindCapture(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition patches = assembly.MainModule.GetType("PortableMusicPlayer.GamePatches");
                MethodDefinition apply = patches?.Methods.SingleOrDefault(method => method.Name == "Apply");
                MethodDefinition applyCapture = patches?.Methods.SingleOrDefault(method => method.Name == "ApplyKeyCapturePatch");
                MethodDefinition capture = patches?.Methods.SingleOrDefault(method => method.Name == "CaptureMouseButton");
                Assert.True(apply != null && References(apply, "ApplyKeyCapturePatch") && applyCapture != null &&
                        applyCapture.Body.Instructions.Any(instruction => string.Equals(instruction.Operand as string, "End", StringComparison.Ordinal)) &&
                        applyCapture.Body.Instructions.Any(instruction => string.Equals(instruction.Operand as string, "Bind", StringComparison.Ordinal)) &&
                        capture != null &&
                        capture.ReturnType.FullName == "System.Boolean" && References(capture, "GetKeyDown") &&
                        References(capture, "Invoke") &&
                        capture.Body.Instructions.Any(instruction => Integer(instruction) == 323) &&
                        capture.Body.Instructions.Any(instruction => Integer(instruction) == 329),
                    loader + " does not extend the framework keybind capture to mouse buttons");
            }
        }

        private static void CheckViewInputPatch(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition patches = assembly.MainModule.GetType("PortableMusicPlayer.GamePatches");
                MethodDefinition apply = patches?.Methods.SingleOrDefault(method => method.Name == "Apply");
                MethodDefinition filter = patches?.Methods.SingleOrDefault(method => method.Name == "FilterViewAxes");
                MethodDefinition axis = patches?.Methods.SingleOrDefault(method => method.Name == "ViewAxis");
                Assert.True(apply != null && apply.Body.Instructions.Any(instruction =>
                        instruction.Operand is TypeReference type && type.FullName == "ECM.Components.MouseLook") &&
                    apply.Body.Instructions.Any(instruction =>
                        instruction.Operand is TypeReference type && type.FullName == "ThirdPersonCameraController"),
                    loader + " does not patch both first- and third-person view rotation");
                Assert.True(filter != null && filter.Body.Instructions.Any(instruction =>
                        instruction.Operand is FieldReference field && field.Name == "FilteredAxis") && axis != null &&
                        axis.ReturnType.FullName == "System.Single" && References(axis, "get_BlocksViewRotation") &&
                        References(axis, "GetAxis"),
                    loader + " view input is not filtered without skipping the camera update");
                Assert.True(patches.Methods.All(method => method.Name != "AllowViewRotation"),
                    loader + " can still skip the whole camera update and make movement catch up later");
            }
        }

        private static void CheckBatteryReplacement(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition action = assembly.MainModule.GetType("PortableMusicPlayer.Items.ReplaceBatteryAction");
                Assert.True(action != null && action.BaseType.FullName == "OS.Items.ItemReloadable",
                    loader + " battery replacement does not use the native reload behavior");
                MethodDefinition canUse = action.Methods.SingleOrDefault(method => method.Name == "CanUse");
                MethodDefinition invoke = action.Methods.SingleOrDefault(method => method.Name == "Invoke");
                MethodDefinition constructor = action.Methods.SingleOrDefault(method => method.IsConstructor);
                Assert.True(constructor != null && constructor.Body.Instructions.Any(instruction =>
                        string.Equals(instruction.Operand as string, "Change batteries", StringComparison.Ordinal)),
                    loader + " battery replacement has the wrong player-facing name");
                Assert.True(canUse != null && References(canUse, "PrepareBatteryReplacement"),
                    loader + " battery availability does not settle deferred charge");
                Assert.True(invoke != null && References(invoke, "PrepareBatteryReplacement") &&
                        References(invoke, "FinishBatteryReplacement"),
                    loader + " battery replacement does not resynchronize the active meter");
                MethodDefinition normalize = assembly.MainModule.GetType("PortableMusicPlayer.Items.ItemSetup")?.Methods
                    .SingleOrDefault(method => method.Name == "NormalizePlayer");
                TypeDefinition power = assembly.MainModule.GetType("PortableMusicPlayer.Items.PowerAction");
                Assert.True(normalize != null && !normalize.Body.Instructions.Any(instruction =>
                        instruction.OpCode.Code == Mono.Cecil.Cil.Code.Newobj && instruction.Operand is MethodReference created &&
                        created.DeclaringType.FullName == action.FullName) && power != null &&
                    power.Methods.Any(method => method.Name == "CanUse" && References(method, "NeedsBatteryReplacement")) &&
                    power.Methods.Any(method => method.Name == "Invoke" && References(method, "Invoke")),
                    loader + " power action does not turn into Change batteries when the AA cell is empty");
            }
        }

        private static void CheckEscapeHandling(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition patches = assembly.MainModule.GetType("PortableMusicPlayer.GamePatches");
                MethodDefinition apply = patches?.Methods.SingleOrDefault(method => method.Name == "Apply");
                MethodDefinition close = patches?.Methods.SingleOrDefault(method => method.Name == "ClosePlayerBeforePause");
                Assert.True(apply != null && apply.Body.Instructions.Any(instruction =>
                        instruction.Operand is TypeReference type && type.FullName == "GameController") &&
                    apply.Body.Instructions.Any(instruction =>
                        instruction.Operand is TypeReference type && type.FullName == "PauseMenu") &&
                    close != null && References(close, "TryCloseFromCancel") && References(close, "GetKeyDown") &&
                    References(close, "get_frameCount"),
                    loader + " Escape does not close the music player before native pause handling");
            }
        }

        private static void CheckGameRadioCollection(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition radio = assembly.MainModule.GetType("PortableMusicPlayer.Library.GameRadioLibrary");
                TypeDefinition obentunes = assembly.MainModule.GetType("PortableMusicPlayer.Library.ObentunesLibrary");
                TypeDefinition player = assembly.MainModule.GetType("PortableMusicPlayer.Playback.MusicPlayer");
                MethodDefinition tick = player?.Methods.SingleOrDefault(method => method.Name == "Tick");
                MethodDefinition start = player?.Methods.SingleOrDefault(method => method.Name == "StartTrack");
                Assert.True(radio != null && radio.Methods.Any(method => method.Name == "Refresh") &&
                    tick != null && References(tick, "Refresh") && start != null && References(start, "TryGetClip") &&
                    References(start, "IsPath") && References(start, "get_IsReady"),
                    loader + " does not expose the game's default radio clips as a playable collection");
                Assert.True(obentunes != null && obentunes.Methods.Any(method => method.Name == "Refresh") &&
                        tick != null && References(tick, "get_IsDetected"),
                    loader + " does not replace Game Radio with all detected Obentunes clips");
                MethodDefinition refresh = radio.Methods.Single(method => method.Name == "Refresh");
                Assert.True(refresh.Body.Instructions.Any(instruction =>
                        string.Equals(instruction.Operand as string, "radio_static_001", StringComparison.OrdinalIgnoreCase)),
                    loader + " still lists the native radio_static_001 noise clip as a song");
            }
        }

        private static void CheckPlayerMessages(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition player = assembly.MainModule.GetType("PortableMusicPlayer.Playback.MusicPlayer");
                MethodDefinition open = player?.Methods.SingleOrDefault(method => method.Name == "OpenWindow");
                MethodDefinition tick = player?.Methods.SingleOrDefault(method => method.Name == "Tick");
                MethodDefinition stop = player?.Methods.SingleOrDefault(method => method.Name == "StopFailedTrack");
                Assert.True(open != null && open.Body.Instructions.Any(instruction =>
                        string.Equals(instruction.Operand as string, "Turn on the music player", StringComparison.Ordinal)),
                    loader + " unpowered-player prompt is not the requested copy");
                Assert.True(tick != null && tick.Body.Instructions.Any(instruction =>
                        string.Equals(instruction.Operand as string,
                            "Music player turned off because it left your inventory.", StringComparison.Ordinal)),
                    loader + " dropped-player message is not a single sentence");
                Assert.True(!player.Methods.SelectMany(method => method.HasBody ? method.Body.Instructions :
                        Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Any(instruction =>
                            string.Equals(instruction.Operand as string, "Music player off: ", StringComparison.Ordinal)),
                    loader + " still contains the colon-style power-off message");
                Assert.True(stop != null && !References(stop, "PowerOff") && !References(stop, "Close") &&
                        !References(stop, "Next") && References(stop, "Notify") && References(stop, "Stop") &&
                        stop.Body.Instructions.Any(instruction => string.Equals(instruction.Operand as string,
                            "This song could not be loaded.", StringComparison.Ordinal)),
                    loader + " a failed song still advances, powers off, or closes the player");
            }
        }

        private static void CheckFrameworkKey(TypeDefinition settings,
            System.Collections.Generic.List<Mono.Cecil.Cil.Instruction> instructions, string id, int expected, string loader)
        {
            PropertyDefinition property = settings?.Properties.SingleOrDefault(candidate => candidate.Name == id);
            Assert.Equal("ObenseuerCore.ModKeybind", property?.PropertyType.FullName,
                loader + " " + id + " framework keybind type");
            int idAt = instructions?.FindIndex(instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr &&
                string.Equals(instruction.Operand as string, id, StringComparison.Ordinal)) ?? -1;
            int registerAt = instructions?.FindIndex(Math.Max(0, idAt), instruction =>
                instruction.Operand is MethodReference method && method.Name == "Register" &&
                method.DeclaringType.FullName == "ObenseuerCore.Keybinds") ?? -1;
            Assert.True(idAt >= 0 && registerAt > idAt,
                loader + " does not register " + id + " in the framework keybind manager");
            Assert.True(instructions.Skip(idAt).Take(registerAt - idAt).Any(instruction =>
                    Integer(instruction) == expected),
                loader + " " + id + " default is not " + expected);
        }

        private static int? Integer(Mono.Cecil.Cil.Instruction instruction)
        {
            switch (instruction.OpCode.Code)
            {
                case Mono.Cecil.Cil.Code.Ldc_I4_M1: return -1;
                case Mono.Cecil.Cil.Code.Ldc_I4_0: return 0;
                case Mono.Cecil.Cil.Code.Ldc_I4_1: return 1;
                case Mono.Cecil.Cil.Code.Ldc_I4_2: return 2;
                case Mono.Cecil.Cil.Code.Ldc_I4_3: return 3;
                case Mono.Cecil.Cil.Code.Ldc_I4_4: return 4;
                case Mono.Cecil.Cil.Code.Ldc_I4_5: return 5;
                case Mono.Cecil.Cil.Code.Ldc_I4_6: return 6;
                case Mono.Cecil.Cil.Code.Ldc_I4_7: return 7;
                case Mono.Cecil.Cil.Code.Ldc_I4_8: return 8;
                case Mono.Cecil.Cil.Code.Ldc_I4_S:
                case Mono.Cecil.Cil.Code.Ldc_I4: return Convert.ToInt32(instruction.Operand);
                default: return null;
            }
        }

        private static void CheckEnvironmentalReverb(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition output = assembly.MainModule.GetType("PortableMusicPlayer.Playback.AudioOutput");
                MethodDefinition constructor = output?.Methods
                    .SingleOrDefault(method => method.IsConstructor && !method.IsStatic);
                var instructions = constructor?.Body.Instructions.ToList();
                int bypassAt = instructions?.FindIndex(instruction => instruction.Operand is MethodReference method &&
                    method.DeclaringType.FullName == "UnityEngine.AudioSource" && method.Name == "set_bypassReverbZones") ?? -1;
                int mixAt = instructions?.FindIndex(instruction => instruction.Operand is MethodReference method &&
                    method.DeclaringType.FullName == "UnityEngine.AudioSource" && method.Name == "set_reverbZoneMix") ?? -1;
                Assert.True(bypassAt > 0 && instructions[bypassAt - 1].OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_1,
                    loader + " private source still enters environmental reverb zones");
                Assert.True(mixAt > 0 && instructions[mixAt - 1].OpCode.Code == Mono.Cecil.Cil.Code.Ldc_R4 &&
                        Convert.ToSingle(instructions[mixAt - 1].Operand) == 0f,
                    loader + " private source keeps a non-zero environmental reverb send");
                Assert.True(!instructions.Any(instruction => instruction.Operand is MethodReference method &&
                        method.DeclaringType.FullName == "UnityEngine.AudioSource" && method.Name == "set_bypassEffects"),
                    loader + " private source bypasses its headphone filters");
                MethodDefinition finished = output?.Methods.SingleOrDefault(method => method.Name == "get_Finished");
                MethodDefinition tick = output?.Methods.SingleOrDefault(method => method.Name == "Tick");
                Assert.True(output?.Fields.Any(field => field.Name == "played") == true &&
                        finished != null && finished.Body.Instructions.Any(instruction =>
                            instruction.Operand is FieldReference field && field.Name == "played") &&
                        tick != null && References(tick, "Fail"),
                    loader + " a direct clip that never starts can still be classified as a finished song");
            }
        }

        private static void CheckWorldModelPresentation(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition itemSetup = assembly.MainModule.GetType("PortableMusicPlayer.Items.ItemSetup");
                MethodDefinition options = itemSetup?.Methods.SingleOrDefault(method => method.Name == "WorldModelOptions");
                Assert.True(options != null, loader + " item setup has no shared world-model presentation");
                var instructions = options.Body.Instructions.ToList();
                int pivotAt = instructions.FindIndex(instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Stfld &&
                    instruction.Operand is FieldReference field && field.DeclaringType.FullName == "ObenseuerCore.ModelOptions" && field.Name == "Pivot");
                int rotationAt = instructions.FindIndex(instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Stfld &&
                    instruction.Operand is FieldReference field && field.DeclaringType.FullName == "ObenseuerCore.ModelOptions" && field.Name == "EulerOffset");
                Assert.True(pivotAt > 0 && instructions.Skip(Math.Max(0, pivotAt - 3)).Take(3).Any(instruction =>
                        instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_1),
                    loader + " world models are not bottom-pivoted");
                Assert.True(rotationAt > 0 && instructions.Skip(Math.Max(0, rotationAt - 8)).Take(8).Any(instruction =>
                        instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_R4 && Convert.ToSingle(instruction.Operand) == -90f),
                    loader + " world models are not rotated flat");
            }
        }

        private static void CheckCatalogueReload(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition plugin = assembly.MainModule.GetType("PortableMusicPlayer.PortableMusicPlayerMod");
                MethodDefinition refresh = plugin?.Methods.SingleOrDefault(method => method.Name == "RefreshItems");
                Assert.True(refresh != null, loader + " plugin has no persistent catalogue-rebuild handler");
                var refreshInstructions = refresh.Body.Instructions.ToList();
                int verifyAt = CallIndex(refreshInstructions, "PortableMusicPlayer.Items.ItemSetup", "Verify");
                int normalizeAt = CallIndex(refreshInstructions, "PortableMusicPlayer.Items.ItemSetup", "Normalize");
                Assert.True(verifyAt >= 0 && normalizeAt > verifyAt,
                    loader + " catalogue rebuild does not verify ownership before normalization");

                MethodDefinition finish = plugin.Methods.Single(method => method.Name == "FinishLoad");
                MethodDefinition destroy = plugin.Methods.Single(method => method.Name == "OnDestroy");
                Assert.True(References(finish, "RefreshItems") && References(finish, "add_Loaded"),
                    loader + " plugin does not subscribe its persistent rebuild handler after initial startup");
                Assert.True(References(destroy, "RefreshItems") && References(destroy, "remove_Loaded"),
                    loader + " plugin does not unsubscribe its persistent rebuild handler on destroy");
            }
        }

        private static bool References(MethodDefinition method, string memberName) => method.Body.Instructions.Any(instruction =>
            instruction.Operand is MethodReference referenced && referenced.Name == memberName);

        private static void CheckStartupFlow(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                TypeDefinition itemSetup = assembly.MainModule.GetType("PortableMusicPlayer.Items.ItemSetup");
                MethodDefinition define = itemSetup?.Methods.SingleOrDefault(method => method.Name == "Define");
                Assert.True(define != null && !define.Body.Instructions.Any(instruction =>
                        instruction.Operand is MethodReference called && called.Name == "add_Loaded"),
                    loader + " subscribes item normalization before catalogue ownership is verified");

                MethodDefinition verify = itemSetup?.Methods.SingleOrDefault(method => method.Name == "Verify");
                bool readsTitle = verify != null && verify.Body.Instructions.Any(instruction =>
                    instruction.Operand is FieldReference field && field.Name == "Title" ||
                    instruction.Operand is MethodReference called && called.Name == "get_Title");
                bool comparesExactly = verify != null && verify.Body.Instructions.Any(instruction =>
                    instruction.Operand is MethodReference called && called.DeclaringType.FullName == "System.String" &&
                    called.Name == "Equals" && called.Parameters.Any(parameter =>
                        parameter.ParameterType.FullName == "System.StringComparison"));
                Assert.True(readsTitle && comparesExactly,
                    loader + " catalogue verification does not require each registered ID's exact expected title");

                MethodDefinition finish = assembly.MainModule.GetType("PortableMusicPlayer.PortableMusicPlayerMod")?.Methods
                    .SingleOrDefault(method => method.Name == "FinishLoad");
                var instructions = finish?.Body.Instructions.ToList();
                int verifyAt = CallIndex(instructions, "PortableMusicPlayer.Items.ItemSetup", "Verify");
                int normalizeAt = CallIndex(instructions, "PortableMusicPlayer.Items.ItemSetup", "Normalize");
                int registerAt = CallIndex(instructions, null, "Register");
                int disposeAt = CallIndex(instructions, "PortableMusicPlayer.Playback.MusicPlayer", "Dispose");
                int commitAt = instructions?.FindLastIndex(instruction =>
                    instruction.OpCode.Code == Mono.Cecil.Cil.Code.Stfld && instruction.Operand is FieldReference field &&
                    field.Name == "initialized") ?? -1;
                Assert.True(verifyAt >= 0 && normalizeAt > verifyAt,
                    loader + " does not normalize items only after catalogue verification");
                Assert.True(registerAt >= 0 && commitAt > registerAt && disposeAt >= 0,
                    loader + " startup does not commit last and dispose its local player on failure");
            }
        }

        private static int CallIndex(System.Collections.Generic.List<Mono.Cecil.Cil.Instruction> instructions,
            string declaringType, string methodName) => instructions?.FindIndex(instruction =>
                instruction.Operand is MethodReference called && called.Name == methodName &&
                (declaringType == null || called.DeclaringType.FullName == declaringType)) ?? -1;

        private static void CheckIdentityWriteback(string loader, string fileName)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(PluginAssembly(loader, fileName)))
            {
                MethodDefinition follow = assembly.MainModule.GetType("PortableMusicPlayer.Playback.ActivePlayer")?.Methods
                    .SingleOrDefault(method => method.Name == "Follow");
                Assert.True(follow != null && follow.ReturnType.FullName == "System.String",
                    loader + " active-player follow does not return the current durable identity");

                MethodDefinition tick = assembly.MainModule.GetType("PortableMusicPlayer.Playback.MusicPlayer")?.Methods
                    .SingleOrDefault(method => method.Name == "Tick");
                bool writesIdentity = tick != null && tick.Body.Instructions.Any(instruction =>
                    instruction.OpCode.Code == Mono.Cecil.Cil.Code.Stfld && instruction.Operand is FieldReference field &&
                    field.DeclaringType.FullName == "PortableMusicPlayer.Core.SavedState" && field.Name == "PlayerId");
                Assert.True(writesIdentity, loader + " playback tick does not persist a repaired active-player identity");
            }
        }

        private static void CheckPlugin(string loader, string fileName, string baseType, string dependencyFolder)
        {
            string pluginPath = PluginAssembly(loader, fileName);
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(dependencyFolder);
            resolver.AddSearchDirectory(Path.GetDirectoryName(pluginPath));
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(pluginPath,
                       new ReaderParameters { AssemblyResolver = resolver }))
            {
                TypeDefinition plugin = assembly.MainModule.Types.SingleOrDefault(type =>
                    type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "BepInEx.BepInPlugin"));
                Assert.True(plugin != null, loader + " mod is not a BepInEx plugin, so item registration still runs after ItemDatabase.Awake");
                Assert.Equal(baseType, plugin.BaseType.FullName, loader + " plugin base type");
                Assert.True(plugin.Methods.Any(method => method.Name == "Awake" && method.Parameters.Count == 0),
                    "the BepInEx plugin has no Awake initializer");

                CustomAttribute dependency = plugin.CustomAttributes.SingleOrDefault(attribute =>
                    attribute.AttributeType.FullName == "BepInEx.BepInDependency");
                Assert.True(dependency != null, "the plugin has no hard dependency on the framework");
                Assert.Equal("obenseuer.moddingframework", dependency.ConstructorArguments[0].Value as string,
                    "framework dependency id");
                Assert.True(!dependency.Constructor.Parameters.Any(parameter => parameter.ParameterType.FullName == "System.Version") &&
                        !dependency.ConstructorArguments.Skip(1).Any(argument => argument.Type.FullName == "System.String"),
                    "the framework dependency is pinned to a version");
                Assert.True(dependency.ConstructorArguments.Count < 2 ||
                        Convert.ToInt32(dependency.ConstructorArguments[1].Value) == 1,
                    "the framework dependency is not hard");
                Assert.True(!plugin.CustomAttributes.Any(attribute =>
                        attribute.AttributeType.FullName == "ObenseuerCore.ModInfoAttribute"),
                    "the BepInEx plugin is still discoverable as a late framework mod");
                Assert.True(!assembly.MainModule.Types.Any(type => type.BaseType?.FullName == "ObenseuerCore.FrameworkMod" ||
                        type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "ObenseuerCore.ModInfoAttribute")),
                    "the assembly still contains a late framework-mod entry point");

                TypeDefinition loot = assembly.MainModule.GetType("PortableMusicPlayer.Items.LootSetup");
                MethodDefinition apply = loot?.Methods.SingleOrDefault(method => method.Name == "Apply");
                Assert.True(apply != null && apply.Body.Instructions.Any(instruction =>
                        instruction.Operand is MethodReference called &&
                        called.DeclaringType.FullName == "PortableMusicPlayer.Items.ItemSetup" && called.Name == "TryIdOf") &&
                    apply.Body.Instructions.Any(instruction =>
                        instruction.Operand is MethodReference called &&
                        called.DeclaringType.FullName == "PortableMusicPlayer.Items.ItemSetup" && called.Name == "IsUsableId"),
                    loader + " loot registration is not guarded by the validated item-ID map");
            }
        }

        private static string PluginAssembly(string loader, string fileName)
        {
            DirectoryInfo directory = new FileInfo(typeof(PluginArchitectureTests).Assembly.Location).Directory;
            while (directory != null && directory.Name != ".local") directory = directory.Parent;
            Assert.True(directory != null, "test assembly is not under the project .local directory");
            string path = Path.Combine(directory.FullName, "bin", "PortableMusicPlayer", loader, "Release", fileName);
            Assert.True(File.Exists(path), "plugin assembly is missing at " + path);
            return path;
        }
    }
}
