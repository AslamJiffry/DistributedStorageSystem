using Grpc.Core;
using Grpc.Net.Client;
using Nodegrpcservice;
using NodeProject;
using System.Runtime.CompilerServices;
public class Program
{
    public static void Main(string[] args)
    {
        var node = new Node();
        node.Start();
    }

    



}
