using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Nodegrpcservice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NodeProject
{
    public class Node
    {
        // -1 indicates no leader has been elected
        public static int LeaderNodeId = -1;
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Role { get; private set; }

        public bool IsLeader { get; set; }
        public bool IsNodeStarted { get; set; }
        public List<NodeTable> NodeTable { get; private set; }
        public List<ValueTable> ValueTable { get; private set; }

        private const string IP = "http://localhost:";
        private const int Port = 50051;

        Server _server;
        NodeDiscoveryService discovery;

        private Dictionary<int, (Channel channel, NodeService.NodeServiceClient client)> _clientConnections = new Dictionary<int, (Channel channel, NodeService.NodeServiceClient client)>();

        public Node()
        {
            IsNodeStarted = false;
            NodeTable = new List<NodeTable>();
            ValueTable = new List<ValueTable>();
            discovery = new NodeDiscoveryService();
            //Task.Run(() => PeriodicallyCheckActiveNodes());
        }

        public async void Start()
        {
            //DetermineRole();
            // Task.Run(() => DetermineRole());

            var registredNode = discovery.RegisterNodeAsync("localhost", Port);
            this.Id = (int)registredNode?.Result?.Id;
            this.Name = (string)registredNode?.Result?.Name;
            var nodePort = (int)registredNode?.Result?.Port;

            _server = new Server
            {
                Services = { NodeService.BindService(new NodeGrpcService()) },
                Ports = { new ServerPort("localhost", nodePort, ServerCredentials.Insecure) }
            };
            _server.Start();

            // Role assignment logic goes here
            // For example, use NodeCount to determine the role
            //Role = (this.Id % 2 == 0) ? "Hasher" : "Receiver";
             
            var nodes = discovery.DiscoverNodesAsync();
            var anodes = nodes.Result.ToList();

            var broadCastResponse = new NodeInfoResponse();
            foreach (var node in anodes)
            {
              var task =  BroadcastNode(IP + node.Port, node.Id,this.Id);
              broadCastResponse = task.Result; 
            }

            foreach (var item in anodes)
            {
                if(item.Id != this.Id)
                this.NodeTable.Add(new NodeTable
                {
                    NodeId = item.Id,
                    Name = item.Name,
                    Port = Port + item.Id,
                    Role = "",
                });
            }

           DetermineRoleAndLeader();

            

            Console.WriteLine($"Node {Id} started with gRPC server listening on port {nodePort}.");
            // Keep the server running until the user presses a key
            Console.WriteLine("Press any key to stop the node...");
            IsNodeStarted = true;
            Console.ReadKey();

            Stop(); // Ensure to stop the server when the Node is being stopped
        }

        public void Stop()
        {
            _server?.ShutdownAsync().Wait();
            discovery.DeregisterNodeAsync(Id);
            Console.WriteLine($"Node {Id} has been stopped.");
        }

        private async void DetermineRoleAndLeader()
        {
            try
            {
                var nodes = await discovery.DiscoverNodesAsync();
                // If this node has the highest ID or no nodes are present, it's the leader.
                IsLeader = !nodes.Any() || Id >= nodes.Max(n => n.Id);
                if (IsLeader)
                {
                    LeaderNodeId = Id;
                    AssignRolesToNodes(nodes);
                    BroadcastLeaderAndRoleInfo();
                }
                else
                {
                    // If not the leader, ask for current leader and roles.
                    RequestLeaderAndRoleInfo();
                }

                foreach (var item in NodeTable)
                {
                    Console.WriteLine(item.NodeId + ":" + "Role Is" + item.Role);
                }
            }
            catch (Exception exc)
            {

                throw;
            }
    
        }

        private void AssignRolesToNodes(List<ClusterNode> nodes)
        {
            nodes = nodes.OrderBy(n => n.CreatedDate).ToList();
            var nodecount = 1;
            foreach (var node in nodes)
            {
                // Assign roles based on the new node count.
                node.Role = nodecount % 2 == 0 ? "Hasher" : "Receiver";

                discovery.UpdateNodeRoleAsync(node.Id, node.Role);

                // Update the node table with new roles here.
                var nodetableItem = NodeTable.FirstOrDefault(e => e.NodeId == node.Id);

                if(nodetableItem != null)
                {
                    nodetableItem.Role = node.Role;
                }
                nodecount++;
            }
        }
        private async Task BroadcastLeaderAndRoleInfo()
        {
            LeaderAndRoleInfo leaderAndRoleInfo = new LeaderAndRoleInfo
            {
                LeaderNodeId = LeaderNodeId,
            };

            // Populate the repeated NodeInfo field with information from your NodeTable.
            foreach (var node in NodeTable)
            {
                leaderAndRoleInfo.Nodes.Add(new NodeInfo
                {
                    NodeId = node.NodeId,
                    Role = node.Role
                });
            }

            // Broadcast the leader and role information to all other nodes.
            foreach (var node in NodeTable)
            {
                try
                {
                    var channel = GrpcChannel.ForAddress(IP + node.Port);
                    var client = new NodeService.NodeServiceClient(channel);

                    var response = await client.BroadcastLeaderAndRoleInfoAsync(leaderAndRoleInfo);

                    if (!response.Success)
                    {
                        Console.WriteLine($"Failed to broadcast leader and role info to node {node.NodeId}: {response.Message}");
                    }
                }
                catch (RpcException rpcEx)
                {
                    // Handle RPC exceptions, such as when a node is unreachable
                    Console.WriteLine($"Error communicating with node {node.NodeId}: {rpcEx.Status}");
                }
                catch (Exception ex)
                {
                    // Handle other exceptions
                    Console.WriteLine($"An error occurred while broadcasting to node {node.NodeId}: {ex.Message}");
                }
            }
        }
        private async void RequestLeaderAndRoleInfo()
        {
            // Go through each node in the NodeTable (except self) to request leader and role info
            foreach (var node in NodeTable)
            {
                try
                {
                    var channel = GrpcChannel.ForAddress(IP+node.Port);
                    var client = new NodeService.NodeServiceClient(channel);

                    
                    var response = await client.GetLeaderAndRoleInfoAsync(new Empty()); 

                    // Process the response to update local leader and roles
                    if (response != null)
                    {
                        LeaderNodeId = response.LeaderNodeId;

                        // Assume we receive a list of NodeInfo with their roles
                        foreach (var nodeInfo in response.Nodes)
                        {
                            // Update local NodeTable with the received roles
                            var localNodeInfo = NodeTable.FirstOrDefault(n => n.NodeId == nodeInfo.NodeId);
                            if (localNodeInfo != null)
                            {
                               localNodeInfo.Role = nodeInfo.Role;
                            }
                        }
                    }
                }
                catch (RpcException rpcEx)
                {
                    // Handle RPC exceptions, such as when a node is unreachable
                    Console.WriteLine($"Error communicating with node {node.NodeId}: {rpcEx.Status}");
                }
                catch (Exception ex)
                {
                    // Handle other exceptions
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
        }

        public static async Task<NodeInfoResponse> BroadcastNode(string targetAddress, int nodeId,int currentNodeId)
        {
            NodeInfoResponse response = new NodeInfoResponse();
            try
            {
                if(nodeId != currentNodeId)
                {
                    var channel = GrpcChannel.ForAddress(targetAddress);
                    var client = new NodeService.NodeServiceClient(channel);

                    response = await client.BroadcastNodeInfoAsync(new NodeInfo { NodeId = nodeId, NodeName = "" });
                    Console.WriteLine($"Node Id : {nodeId} Brodcasted successfully: {response.Nodes}");
                }
            }
            catch (Exception exc)
            {
                throw exc;
            }

            return response;
        }

        public static async Task<HeartbeatResponse> SendHeartbeat(string address,int nodeId, string nodeName)
        {
            HeartbeatResponse response = new HeartbeatResponse();
            try
            {
                    var channel = GrpcChannel.ForAddress(address);
                    var client = new NodeService.NodeServiceClient(channel);

                    response = await client.SendHeartbeatAsync(new HeartbeatRequest { NodeId = nodeId, NodeName = "" });
                    Console.WriteLine($"Node Id : {nodeId} is alive {response.Success}");
            }
            catch (Exception exc)
            {
                throw exc;
            }

            return response;
        }

        async Task PeriodicallyCheckActiveNodes()
        {
            while (true)
            {
                if (IsNodeStarted)
                {
                    foreach (var item in NodeTable)
                    {
                        var response = SendHeartbeat(IP + item.Port, item.NodeId, item.Name);

                        if (!response.Result.Success)
                        {

                        }
                    }
                    await Task.Delay(TimeSpan.FromMinutes(1)); // Check every minute
                }
            }
        }

        public List<NodeTable> AddToNodeTable(NodeTable table)
        {
            this.NodeTable.Add(table);
            return this.NodeTable;
        }

        public List<NodeTable> RemoveFromNodeTable(NodeTable table)
        {
            this.NodeTable.Add(table);
            return this.NodeTable;
        }
    }
}
