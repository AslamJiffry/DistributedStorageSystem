using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Nodegrpcservice;
using System.Runtime.CompilerServices;


namespace NodeProject
{
    public class NodeGrpcService : NodeService.NodeServiceBase
    {
       public static readonly Dictionary<int, NodeDetails> _nodes = new Dictionary<int, NodeDetails>();
       public static readonly LeaderAndRoleInfo leaderAndRoleInfo = new LeaderAndRoleInfo();
       
       
        public async override Task<NodeInfoResponse> BroadcastNodeInfo(NodeInfo request, ServerCallContext context)
        {
            // Store or update the node info.
            _nodes[request.NodeId] = new NodeDetails
            {
                NodeInfo = request,
                LastHeartbeat = DateTime.UtcNow
            };
            
            NodeInfoResponse response = new NodeInfoResponse();
            foreach (var node in _nodes.Values)
            {
                response.Nodes.Add(node.NodeInfo); 
            }

            // Return the list of all nodes.
            return response;
        }

        // Implementing GetLeaderAndRoleInfo
        public override Task<LeaderAndRoleInfoResponse> GetLeaderAndRoleInfo(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
        {
            // Example implementation - should be replaced with actual logic
            //response = new LeaderAndRoleInfoResponse { LeaderNodeId = 1, Nodes = new List<NodeInfo>() };
            return Task.FromResult(new LeaderAndRoleInfoResponse());
        }

        public override Task<BroadcastResponse> BroadcastLeaderAndRoleInfo(LeaderAndRoleInfo request, ServerCallContext context)
        {
            leaderAndRoleInfo.LeaderNodeId = request.LeaderNodeId;

          
            foreach (var node in request.Nodes)
            {
                leaderAndRoleInfo.Nodes.Add(new NodeInfo() { 
                         NodeId = node.NodeId,
                         NodeName = node.NodeName,
                         Role = node.Role
                });
            }

            var response = new BroadcastResponse
            {
                Success = true,
                Message = "Leader and role information broadcasted successfully."
            };

            return Task.FromResult(response);
        }

        public override Task<HeartbeatResponse> SendHeartbeat(HeartbeatRequest request, ServerCallContext context)
        {
            Console.WriteLine($"Received heartbeat from Node ID {request.NodeId}, Name {request.NodeName}");

            // Update the last heartbeat time for the node
            lock (_nodes) // Ensure thread safety
            {
                if (_nodes.TryGetValue(request.NodeId, out NodeDetails nodeDetails))
                {
                    nodeDetails.LastHeartbeat = DateTime.UtcNow;
                }
            }

            return Task.FromResult(new HeartbeatResponse { Success = true });
        }

        public static void CheckActiveNodes()
        {
            var threshold = TimeSpan.FromMinutes(5);
            var now = DateTime.UtcNow;

            List<int> inactiveNodeIds = new List<int>();

            lock (_nodes) // Ensure thread safety
            {
                foreach (var kvp in _nodes)
                {
                    if (now - kvp.Value.LastHeartbeat > threshold)
                    {
                        // This node is considered inactive
                        inactiveNodeIds.Add(kvp.Key);
                        Console.WriteLine($"Node ID {kvp.Key}, Name {kvp.Value.NodeInfo.NodeName} is inactive.");
                    }
                    else
                    {
                        Console.WriteLine($"Node ID {kvp.Key}, Name {kvp.Value.NodeInfo.NodeName} is active.");
                    }
                }

                // Optionally remove inactive nodes from the dictionary
                foreach (var nodeId in inactiveNodeIds)
                {
                    _nodes.Remove(nodeId);
                }
            }
        }

    }
}
