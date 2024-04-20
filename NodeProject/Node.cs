using Grpc.Core;
using Grpc.Net.Client;
using Nodegrpcservice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Net.WebRequestMethods;

namespace NodeProject
{
    public class Node
    {
        // -1 indicates no leader has been elected
        public static int LeaderNodeId = -1; 
        public int Id { get;  set; }
        public string Name { get;  set; }
        public string Role { get;  set; }

        public bool IsLeader { get;  set; }
        public bool IsNodeStarted { get; set; }
        public List<NodeTable> NodeTable { get; set; }
        public List<ValueTable> ValueTable { get;  set; }

        private const string IP = "http://localhost:";
        private const int Port = 50051;

        Server _server;
        NodeDiscoveryService discovery;
        NodeGrpcService nodeGrpcService;

        private Dictionary<int, (Channel channel, NodeService.NodeServiceClient client)> _clientConnections = new Dictionary<int, (Channel channel, NodeService.NodeServiceClient client)>();

        public Node()
        {
            IsNodeStarted = false;
            NodeTable = new List<NodeTable>();
            ValueTable = new List<ValueTable>();
            discovery = new NodeDiscoveryService();
            //Task.Run(() => PeriodicallyCheckActiveNodes());
        }

        public void Start()
        {
            //DetermineRole();
            // Task.Run(() => DetermineRole());

            var registredNode = discovery.RegisterNodeAsync(IP, Port);
            this.Id = (int)registredNode?.Result?.Id;
            var nodePort = Port + this.Id;

            this.Name = "Node-0" + Id;

            _server = new Server
            {
                Services = { NodeService.BindService(new NodeGrpcService()) },
                Ports = { new ServerPort("localhost", nodePort, ServerCredentials.Insecure) }
            };
            _server.Start();
             
            var nodes = discovery.DiscoverNodesAsync();
            var anodes = nodes.Result.ToList();

            var broadCastResponse = new NodeInfoResponse();
            foreach (var node in anodes)
            {
              var np = Port + node.Id;
              var task =  BroadcastNode(IP + np, node.Id,this.Id);

                if (task.Result.Nodes.Count > 0)
                    broadCastResponse.Nodes.Add(task.Result.Nodes.FirstOrDefault());
            }

            foreach (var item in broadCastResponse.Nodes)
            {
                this.NodeTable.Add(new NodeTable
                {
                    NodeId = item.NodeId,
                    Name = item.NodeName,
                    Port = Port + item.NodeId,
                    Role = "",
                });
            }

            this.ElectLeader();

            Console.WriteLine($"Node {Id} started with gRPC server listening on port {nodePort}.");
            // Keep the server running until the user presses a key
            Console.WriteLine("Press any key to stop the node...");
            IsNodeStarted = true;
            Console.ReadKey();

            Stop(); // Ensure to stop the server when the Node is being stopped
        }

        public void Stop()
        {

            foreach (var clientConnection in _clientConnections.Values)
            {
                clientConnection.channel.ShutdownAsync().Wait();
            }
            _clientConnections.Clear();

            _server?.ShutdownAsync().Wait();
            discovery.DeregisterNodeAsync(Id);
            Console.WriteLine($"Node {Id} has been stopped.");
        }

        public async void ElectLeader()
        {
            //lowest ID as the leader
            int? electedLeaderId = NodeTable.Min(node => (int?)node.NodeId);

            if (electedLeaderId.HasValue)
            {
                LeaderNodeId = electedLeaderId.Value;
                IsLeader = LeaderNodeId == this.Id;

                // If this node is the leader, assign roles to all nodes including itself
                if (IsLeader)
                {
                    await BroadcastLeaderElectionResult(IP,Port);
                    await AssignAndBroadcastRoles(); // This will handle role assignment
                }
            }

        }

        public async Task UpdateNodeRole(string targetAddress,int nodeId, string role)
        {
            try
            {
               var channel = GrpcChannel.ForAddress(targetAddress);
               var client = new NodeService.NodeServiceClient(channel);

               var response = await client.SetRoleAsync(new SetRoleRequest { NodeId = nodeId, Role = role });
               Console.WriteLine($"Node Id : {nodeId} Brodcasted successfully: {response.Success}");

               if (response.Success)
               {
                   var node = NodeTable.FirstOrDefault(node => node.NodeId == nodeId);
                   if (node != null)
                   {
                      node.Role = role;
                   }
                   Console.WriteLine($"Role {role} set successfully on Node {nodeId}.");
               }
               else
               {
                   Console.WriteLine($"Failed to set role on Node {nodeId}.");
               }

            }
            catch (Exception exc)
            {
                throw exc;
            }
        }
        public async Task AssignAndBroadcastRoles()
        {
            // Determine and assign roles based on the count of nodes
            int nodeCount = NodeTable.Count + 1; // +1 to include this node itself
            string newRole;

            // Determine the new role for the joining node
            if (nodeCount % 2 == 0)
            {
                newRole = "Hasher";
            }
            else
            {
                newRole = "Receiver";
            }

            var nodePort = Port + this.Id;
            // Assign the new role to the joining node
            await UpdateNodeRole(IP+nodePort,this.Id, newRole);

            // Broadcast the new role assignments to all nodes
            foreach (var node in NodeTable)
            {
                var nPort = Port + node.NodeId;
                string role = (node.NodeId % 2 == 0) ? "Hasher" : "Receiver";
                await UpdateNodeRole(IP+nPort,node.NodeId, role);
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

    public async Task BroadcastLeaderElectionResult(string address,int port)
    {
        // Logic for broadcasting the election result to all nodes goes here
        var leaderInfo = new LeaderInfo { LeaderNodeId = LeaderNodeId, LeaderNodeName = this.Name };

        foreach (var node in NodeTable)
        {
            if (node.NodeId != this.Id) // Don't broadcast to self
            {
                try
                {
                        var nodePort = port + node.NodeId;
                        var channel = GrpcChannel.ForAddress(address+nodePort);
                        var client = new NodeService.NodeServiceClient(channel);
                        var response = await client.BroadcastLeaderInfoAsync(leaderInfo); 

                        if (response != null && response.Success)
                        {
                            // Update the leader node information based on the broadcast
                             //LeaderNodeId = request.LeaderNodeId;
                            //_node.IsLeader = (Node.LeaderNodeId == _node.Id);
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting leader to Node {node.NodeId}: {ex.Message}");
                }
            }
        }
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
