using System.IO;
using System.Linq;
using Mono.Cecil;

namespace PortableMusicPlayer.Tests
{
    // The game members that the patches and reflection depend on, read from the installed game.
    [TestSuite]
    public static class GameShapeTests
    {
        public static void TheChargerExclusionTargetsStillExist()
        {
            using (AssemblyDefinition game = Game())
            {
                TypeDefinition charger = Type(game, "BatteryPowerSource");
                Method(charger, "CheckItems");
                Method(charger, "PowerOFF");
                Field(charger, "fuelStacks", "System.Collections.Generic.List`1<ItemStack>");
                Field(charger, "fuelDrainPerHour", "System.Single");
            }
        }

        public static void TheEarmuffMuffleTargetsStillExist()
        {
            using (AssemblyDefinition game = Game())
            {
                Method(Type(game, "HeadSlotController"), "OnDropped", "Item", "System.Int32", "System.Int32", "System.Object[]");
                Field(Type(game, "CharacterSlotController"), "currentItem", "Item");
                TypeDefinition underwater = Type(game, "UnderwaterEffect");
                Method(underwater, "Muffle");
                Assert.True(underwater.Fields.Any(f => f.Name == "instance" && f.IsStatic), "UnderwaterEffect.instance is missing");
            }
        }

        public static void ThePauseMenuCheckStillExists()
        {
            using (AssemblyDefinition game = Game())
            {
                TypeDefinition pause = Type(game, "PauseMenu");
                Method(pause, "PauseIsActive");
                Assert.True(pause.Fields.Any(f => f.Name == "instance" && f.IsStatic), "PauseMenu.instance is missing");
            }
        }

        public static void TheInventoryDragFieldsStillExist()
        {
            using (AssemblyDefinition game = Game())
            {
                TypeDefinition itemData = Type(game, "ItemData");
                Assert.True(itemData.Fields.Any(f => f.Name == "globaldrag" && f.IsStatic && f.FieldType.Name == "ItemData"), "ItemData.globaldrag is missing");
                Field(itemData, "lastSlotController", "SlotController");
            }
        }

        public static void ItemIdentityMetadataStillClonesAndCanBeHidden()
        {
            using (AssemblyDefinition game = Game())
            {
                TypeDefinition item = Type(game, "Item");
                Method(item, "CloneMeta", "System.Boolean", "System.Int32", "Item");
                Type(game, "OS.Items.ItemTextData");
                Method(Type(game, "ToolTip"), "TextDataToString", "OS.Items.ItemTextData");
            }
        }

        private static AssemblyDefinition Game() =>
            AssemblyDefinition.ReadAssembly(Path.Combine(Program.GamePath, "Obenseuer_Data", "Managed", "Assembly-CSharp.dll"));

        private static TypeDefinition Type(AssemblyDefinition game, string name)
        {
            TypeDefinition type = game.MainModule.GetType(name);
            Assert.True(type != null, name + " is missing");
            return type;
        }

        private static void Method(TypeDefinition type, string name, params string[] parameters) =>
            Assert.True(type.Methods.Any(m => m.Name == name && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters)),
                type.Name + "." + name + "(" + string.Join(", ", parameters) + ") is missing");

        private static void Field(TypeDefinition type, string name, string fieldType) =>
            Assert.True(type.Fields.Any(f => f.Name == name && f.FieldType.FullName == fieldType), type.Name + "." + name + " (" + fieldType + ") is missing");
    }
}
