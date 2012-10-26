using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyClient
{
    //class MyPacketWrapper:IPacketWrapper
    [Serializable]
    public class MyPacketWrapper
    {
        public byte[] FileBuff { get; set; }
        public string FileName { get; set; }
        public UserInfo UserDetails { get; set; }
    }
}
