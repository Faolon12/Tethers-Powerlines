using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace FaolonTether
{
    [ProtoContract]
    public class PowerlineLinks
    {
        [ProtoMember(1)]
        public List<PowerlineLink> Links = new List<PowerlineLink>();
    }
}
