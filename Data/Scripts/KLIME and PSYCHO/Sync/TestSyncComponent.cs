//THIS WAS A TEST FILE, NOT FOR USE IN THIS MOD
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using KlimeAndPsycho.PowerCables.Sync;


namespace KlimeAndPsycho.PowerCables.Sync
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Gyro), false)]
    // Note, your component must implement IMyEventProxy for the game to detect you want to use MySync.
    // Don't worry, there's nothing you need to implement from it, it just needs to be present.
    public class TestSyncComponent : MyGameLogicComponent, IMyEventProxy
    {
        // Note, MySync variables must not be assigned a value (other than null).
        // This value can be set by the server or the client (i.e. terminal actions)
        MySync<bool, SyncDirection.BothWays> m_clientSync = null;

        // This value can only be set by the server (i.e. server validated data)
        MySync<bool, SyncDirection.FromServer> m_serverSync = null;

        static bool m_controlsCreated = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            // The sync variables are already set by the time we get here.
            // Hook the ValueChanged event, so we can do something when the data changes.
            m_clientSync.ValueChanged += clientSync_ValueChanged;
            m_serverSync.ValueChanged += serverSync_ValueChanged;

            // This is a test of SyncExtentions whitelist, however this will execute if you call m_clientSync.ValidateAndSet().
            m_clientSync.AlwaysReject();

            NeedsUpdate |= VRage.ModAPI.MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        private void serverSync_ValueChanged(MySync<bool, SyncDirection.FromServer> obj)
        {
            if (MyAPIGateway.Session.OnlineMode != VRage.Game.MyOnlineModeEnum.OFFLINE && MyAPIGateway.Session.IsServer)
                MyAPIGateway.Utilities.SendMessage($"Synced server value on server: {obj.Value}");
            else
                MyAPIGateway.Utilities.ShowMessage("Test", $"Synced server value on client: {obj.Value}");
        }

        private void clientSync_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            if (MyAPIGateway.Session.OnlineMode != VRage.Game.MyOnlineModeEnum.OFFLINE && MyAPIGateway.Session.IsServer)
                MyAPIGateway.Utilities.SendMessage($"Synced client value on server: {obj.Value}");
            else
                MyAPIGateway.Utilities.ShowMessage("Test", $"Synced client value on client: {obj.Value}");
        }

        static void CreateTerminalControls()
        {
            if (!m_controlsCreated)
            {
                m_controlsCreated = true;

                var clientSyncTestOnOff = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyGyro>("Gwindalmir.Sync.TestClient");
                clientSyncTestOnOff.Enabled = (b) => true;
                clientSyncTestOnOff.Visible = (b) => true;
                clientSyncTestOnOff.Title = MyStringId.GetOrCompute("Client Sync");
                clientSyncTestOnOff.Getter = (b) => b.GameLogic.GetAs<TestSyncComponent>().m_clientSync;
                clientSyncTestOnOff.Setter = (b, v) => b.GameLogic.GetAs<TestSyncComponent>().m_clientSync.Value = v;
                clientSyncTestOnOff.OnText = MyStringId.GetOrCompute("On");
                clientSyncTestOnOff.OffText = MyStringId.GetOrCompute("Off");
                MyAPIGateway.TerminalControls.AddControl<IMyGyro>(clientSyncTestOnOff);

                var serverSyncTestOnOff = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyGyro>("Gwindalmir.Sync.TestServer");
                serverSyncTestOnOff.Enabled = (b) => true;
                serverSyncTestOnOff.Visible = (b) => true;
                serverSyncTestOnOff.Title = MyStringId.GetOrCompute("Server Sync");
                serverSyncTestOnOff.Getter = (b) => b.GameLogic.GetAs<TestSyncComponent>().m_serverSync;
                serverSyncTestOnOff.Setter = (b, v) => b.GameLogic.GetAs<TestSyncComponent>().m_serverSync.Value = v;
                serverSyncTestOnOff.OnText = MyStringId.GetOrCompute("On");
                serverSyncTestOnOff.OffText = MyStringId.GetOrCompute("Off");
                MyAPIGateway.TerminalControls.AddControl<IMyGyro>(serverSyncTestOnOff);
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            CreateTerminalControls();

            (Entity as IMyGyro).EnabledChanged += TestSyncComponent_EnabledChanged;
        }

        private void TestSyncComponent_EnabledChanged(IMyCubeBlock obj)
        {
            // Set the server-side value when the block is turned on/off
            if (MyAPIGateway.Session.IsServer)
                m_serverSync.Value = obj.IsWorking;
        }
    }
}
