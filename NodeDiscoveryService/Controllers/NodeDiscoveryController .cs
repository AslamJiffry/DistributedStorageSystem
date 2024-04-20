using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;

namespace NodeDiscoveryService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NodeDiscoveryController : ControllerBase
    {
        private static List<NodeInfo> Nodes = new List<NodeInfo>();
        private static List<int> availableIds = new List<int> { 0, 1, 2, 3, 4 };

        [HttpPost("register")]
        public IActionResult RegisterNode(NodeInfo nodeInfo)
        {
            if (availableIds.Count == 0)
            {
                return StatusCode(503, "Cluster is full"); // Service Unavailable
            }

            var nodeId = availableIds.First();
            availableIds.Remove(nodeId);

            var node = new NodeInfo { Id = nodeId,Name="",Role="", Address = nodeInfo.Address, Port = nodeInfo.Port+nodeId };
            Nodes.Add(node); // Use Address or a unique identifier for the dictionary key

            return Ok(node);
        }

        [HttpGet("discover")]
        public IActionResult DiscoverNodes()
        {
            return Ok(Nodes);
        }

        [HttpPost("deregister")]
        public IActionResult DeregisterNode(NodeInfo nodeInfo)
        {
            Console.WriteLine("Inside deregistered");
            var node = Nodes.FirstOrDefault(n => n.Id == nodeInfo.Id);
            Console.WriteLine(node);
            if (node != null)
            { 
                Console.WriteLine("Inside if");
            Nodes.Remove(node);
                availableIds.Add(node.Id);
                availableIds.Sort(); // Keep the list sorted for predictable ID assignment
                Console.WriteLine("Node deregistered successfully");
                return Ok("Node deregistered successfully.");
            }

            return NotFound("Node not found.");
        }
    }
}
