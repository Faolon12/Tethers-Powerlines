using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.IO;
using VRage.Utils;

namespace FaolonTether
{
    [ProtoContract]
    public class Settings
    {
        public const string ModName = "FaolonTether";
        public const string Filename = "settings.cfg";

        private static Settings instance;

        public static Settings Instance
        {
            get
            {
                if (instance == null)
                    instance = Load();

                return instance;
            }
        }

        [ProtoMember(1)]
        public float InteractionDistance { get; set; }

        [ProtoMember(2)]
        public float MaxCableDistanceStaticToStatic { get; set; }

        [ProtoMember(3)]
        public float MaxCableDistanceLargeToLarge { get; set; }

        [ProtoMember(4)]
        public float MaxCableDistanceSmallToSmall { get; set; }

        [ProtoMember(5)]
        public float MaxCableDistanceSmallToLarge { get; set; }

        [ProtoMember(6)]
        public float PlayerDrawDistance { get; set; }

        [ProtoMember(7)]
        public int MaxConnectionsChargingStation { get; set; }

        [ProtoMember(8)]
        public int MaxConnectionsTransformerPylon { get; set; }

        [ProtoMember(9)]
        public int MaxConnectionsPowerlinePillar { get; set; }

        [ProtoMember(10)]
        public int MaxConnectionsPowerSockets { get; set; }

        [ProtoMember(11)]
        public int MaxConnectionsConveyorHoseAttachment { get; set; }

        public static Settings GetDefaults()
        {
            return new Settings
            {
                InteractionDistance = 3.5f,
                MaxCableDistanceStaticToStatic = 200f,
                MaxCableDistanceLargeToLarge = 100f,
                MaxCableDistanceSmallToSmall = 50f,
                MaxCableDistanceSmallToLarge = 25f,
                PlayerDrawDistance = 3000f,
                MaxConnectionsChargingStation = 1,
                MaxConnectionsTransformerPylon = 3,
                MaxConnectionsPowerlinePillar = 3,
                MaxConnectionsPowerSockets = 2,
                MaxConnectionsConveyorHoseAttachment = 1
            };
        }

        public static Settings Load()
        {
            Settings defaults = GetDefaults();
            Settings settings = defaults;
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings)))
                {
                    MyLog.Default.Info($"[{ModName}] Loading saved settings");
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(Settings));
                    string text = reader.ReadToEnd();
                    reader.Close();

                    settings = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);

                    // Manually update missing parameters
                    bool updated = false;
                    if (settings.InteractionDistance == 0) { settings.InteractionDistance = defaults.InteractionDistance; updated = true; }
                    if (settings.MaxCableDistanceStaticToStatic == 0) { settings.MaxCableDistanceStaticToStatic = defaults.MaxCableDistanceStaticToStatic; updated = true; }
                    if (settings.MaxCableDistanceLargeToLarge == 0) { settings.MaxCableDistanceLargeToLarge = defaults.MaxCableDistanceLargeToLarge; updated = true; }
                    if (settings.MaxCableDistanceSmallToSmall == 0) { settings.MaxCableDistanceSmallToSmall = defaults.MaxCableDistanceSmallToSmall; updated = true; }
                    if (settings.MaxCableDistanceSmallToLarge == 0) { settings.MaxCableDistanceSmallToLarge = defaults.MaxCableDistanceSmallToLarge; updated = true; }
                    if (settings.PlayerDrawDistance == 0) { settings.PlayerDrawDistance = defaults.PlayerDrawDistance; updated = true; }
                    if (settings.MaxConnectionsChargingStation == 0) { settings.MaxConnectionsChargingStation = defaults.MaxConnectionsChargingStation; updated = true; }
                    if (settings.MaxConnectionsTransformerPylon == 0) { settings.MaxConnectionsTransformerPylon = defaults.MaxConnectionsTransformerPylon; updated = true; }
                    if (settings.MaxConnectionsPowerlinePillar == 0) { settings.MaxConnectionsPowerlinePillar = defaults.MaxConnectionsPowerlinePillar; updated = true; }
                    if (settings.MaxConnectionsPowerSockets == 0) { settings.MaxConnectionsPowerSockets = defaults.MaxConnectionsPowerSockets; updated = true; }
                    if (settings.MaxConnectionsConveyorHoseAttachment == 0) { settings.MaxConnectionsConveyorHoseAttachment = defaults.MaxConnectionsConveyorHoseAttachment; updated = true; }

                    if (updated)
                    {
                        Save(settings);
                        MyLog.Default.Info($"[{ModName}] Settings updated with missing parameters");
                    }
                }
                else
                {
                    MyLog.Default.Info($"[{ModName}] Config file not found. Loading default settings");
                    Save(settings);
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Info($"[{ModName}] Failed to load saved configuration. Loading defaults\n {e.ToString()}");
                Save(settings);
            }

            return settings;
        }

        public static void Save(Settings settings)
        {
            try
            {
                MyLog.Default.Info($"[{ModName}] Saving Settings");
                TextWriter writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(Settings));
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                writer.Close();
            }
            catch (Exception e)
            {
                MyLog.Default.Info($"[{ModName}] Failed to save settings\n{e.ToString()}");
            }
        }
    }
}
