using System.Text;
using TcpSharp;
namespace ConsoleApp1
{
    internal class Program
    {
        static string id = string.Empty;
        static void Main(string[] args)
        {
            TcpSharpSocketClient client = new TcpSharpSocketClient();
            TcpSharpSocketServer server = new TcpSharpSocketServer(2001);

            client.OnConnected += (s, e) => { Console.WriteLine("成功链接服务器");};
            client.OnDisconnected += (s, e) => { Console.WriteLine("与服务器链接断开"); };
            client.OnDataReceived += (s, e) => { Console.WriteLine(Encoding.UTF8.GetString(e.Data)); };
            server.OnConnected += (s, e) => { Console.WriteLine("成功链接客户端"); id = e.ConnectionId; };
            server.OnDisconnected += (s, e) => { Console.WriteLine("与客户端链接断开"); };
            server.StartListening();
            client.Port = 2001;
            client.Host = "127.0.0.1";

            client.Connect();
            Console.ReadLine();
            server.SendString(id,"卧槽");
            client.Disconnect();
            Console.ReadLine();
        }
    }
}
