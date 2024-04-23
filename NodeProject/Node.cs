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
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Role { get; private set; }

        public bool IsLeader { get; set; }
        public bool IsNodeStarted { get; set; }
        public List<NodeTable> NodeTable { get; private set; }
        public List<ValueTable> ValueTable { get; private set; }

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

        public void Start()
        {
            //DetermineRole();
            // Task.Run(() => DetermineRole());

            var registredNode = discovery.RegisterNodeAsync("localhost", Port);
            this.Id = (int)registredNode?.Result?.Id;
            var nodePort = Port + this.Id;

            _server = new Server
            {
                Services = { NodeService.BindService(new NodeGrpcService()) },
                Ports = { new ServerPort("localhost", nodePort, ServerCredentials.Insecure) }
            };
            _server.Start();

            // Role assignment logic goes here
            // For example, use NodeCount to determine the role
            Role = (this.Id % 2 == 0) ? "Hasher" : "Receiver";
             
            var nodes = discovery.DiscoverNodesAsync();
            var anodes = nodes.Result.ToList();

            var broadCastResponse = new NodeInfoResponse();
            foreach (var node in anodes)
            {
              var task =  BroadcastNode("http://localhost:" + node.Port, node.Id,this.Id);
              broadCastResponse = task.Result; 
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

        private void DetermineRole()
        {
            // Here we assume that we have some way of discovering other nodes in the network,
            // for example through a discovery service or a predefined list of nodes.
            //var peerNodes = await DiscoverPeerNodesAsync();

            // Broadcast message to all connected peers and wait for their replies
            //var peersInfo = await BroadcastAndGetPeersInfoAsync(peerNodes);

            // Update node table with received info
            //foreach (var peerInfo in peersInfo)
            //{
            //    NodeTable[peerInfo.Id] = peerInfo.Name;
            //}

            // Determine role based on the number of nodes including this node
            //int totalNodes = NodeTable.Count + 1; // Include this node
            //Role = (totalNodes % 2 == 0) ? "Hasher" : "Receiver";

            //Console.WriteLine($"Node {Id} has determined its role as {Role}.");
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
                        var response = SendHeartbeat("http://localhost:" + item.Port, item.NodeId, item.Name);

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
