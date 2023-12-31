using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;

using FaolonTether.PowerCables.Sync;

namespace FaolonTether.PowerCables
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class PowerCableMod : MySessionComponentBase
    {
        public static PowerCableMod Instance;

        public bool ControlsCreated = false;
        public bool CockpitControlsCreated = false;
        public bool RemoteControlsCreated = false;
        public Networking Networking = new Networking(58936);
        public List<MyEntity> Entities = new List<MyEntity>();
        public PacketBlockSettings CachedPacketSettings;

        public readonly MyStringId MATERIAL_SQUARE = MyStringId.GetOrCompute("Square");
        public readonly MyStringId MATERIAL_DOT = MyStringId.GetOrCompute("WhiteDot");

        public override void LoadData()
        {
            Instance = this;

            Networking.Register();

            CachedPacketSettings = new PacketBlockSettings();
        }

        protected override void UnloadData()
        {
            Instance = null;

            Networking?.Unregister();
            Networking = null;
        }
    }
}
