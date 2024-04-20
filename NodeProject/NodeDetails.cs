using Nodegrpcservice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeProject
{
    public class NodeDetails
    {
            public NodeInfo NodeInfo { get; set; }
            public int Port { get; set; }
            public string Role { get; set; }
            public DateTime LastHeartbeat { get; set; }
      
    }
}
