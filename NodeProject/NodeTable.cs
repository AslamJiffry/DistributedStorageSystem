using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeProject
{
    public class NodeTable
    {
        public int NodeId {  get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public int Port { get; set; }
        public DateTime LastHeartBeat { get; set; }

    }
}
