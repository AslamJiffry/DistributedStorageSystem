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
            try
            {
                var nodeInfo = new
                {
                    Id = 0,
                    Name = "",
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
            catch (Exception exc)
            {

                throw exc;
            }
            
        }

        public async Task<List<ClusterNode>> DiscoverNodesAsync()
        {
            try
            {
                List<ClusterNode> nodeList = new List<ClusterNode>();
                HttpResponseMessage response = await client.GetAsync("https://localhost:7078/api/NodeDiscovery/discover");
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                nodeList = JsonConvert.DeserializeObject<List<ClusterNode>>(responseBody);

                Console.WriteLine("Discover Response: " + responseBody);

                return nodeList;
            }
            catch (Exception exc)
            {

                throw exc;
            }
        }

        public async Task DeregisterNodeAsync(int nodeId)
        {
            try
            {
                //using statement - content is disposed of properly
                using (var content = new StringContent(JsonConvert.SerializeObject(nodeId), Encoding.UTF8, "application/json"))
                {
                    
                    HttpResponseMessage response = await client.PostAsync("https://localhost:7078/api/NodeDiscovery/deregister", content);

                    // Ensure the response status code indicates success
                    response.EnsureSuccessStatusCode();

                    // Read the response content as a string asynchronously
                    string responseString = await response.Content.ReadAsStringAsync();

                    Console.WriteLine("Deregister Response: " + responseString);
                }
            }
            catch (Exception exc)
            {
                // Re-throw the current exception without resetting its stack trace
                throw;
            }

        }
    }
}
