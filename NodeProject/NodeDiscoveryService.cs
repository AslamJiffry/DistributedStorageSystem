using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NodeProject
{
    public  class NodeDiscoveryService
    {
        private static readonly HttpClient client = new HttpClient();
        public async Task<ClusterNode> RegisterNodeAsync(string address, int port)
        {
            var nodeInfo = new
            {
                Id = 0,
                Name ="",
                Role = "",
                Address = address, 
                Port = port
            };

            string json = JsonConvert.SerializeObject(nodeInfo);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync("https://localhost:7078/api/NodeDiscovery/register", content);
            string responseString = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Register Response: " + responseString);

            return JsonConvert.DeserializeObject<ClusterNode>(responseString);
        }

        public async Task<List<ClusterNode>> DiscoverNodesAsync()
        {
            List<ClusterNode> nodeList = new List<ClusterNode>();
            HttpResponseMessage response = await client.GetAsync("https://localhost:7078/api/NodeDiscovery/discover");
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            nodeList = JsonConvert.DeserializeObject<List<ClusterNode>>(responseBody);

            Console.WriteLine("Discover Response: " + responseBody);

            return nodeList;
        }

        public async Task DeregisterNodeAsync(int nodeId)
        {
            var nodeInfo = new
            {
                Id = nodeId,
                Name = "",
                Role = "",
                Address = "",
                Port = 0
            };
            var content = new StringContent(JsonConvert.SerializeObject(nodeInfo), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync("https://localhost:7078/api/NodeDiscovery/deregister", content);
            string responseString = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Deregister Response: " + responseString);
        }
    }
}
