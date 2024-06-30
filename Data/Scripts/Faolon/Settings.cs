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
        public float InteractionDistance;

        [ProtoMember(2)]
        public float MaxCableDistanceStaticToStatic;

        [ProtoMember(3)]
        public float MaxCableDistanceLargeToLarge;

        [ProtoMember(4)]
        public float MaxCableDistanceSmallToSmall;

        [ProtoMember(5)]
        public float MaxCableDistanceSmallToLarge;

        [ProtoMember(6)]
        public float PlayerDrawDistance;

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