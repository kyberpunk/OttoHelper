using System;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Solid.Arduino;

namespace OttoHelper
{
    class Program
    {
        private static ISerialConnection _connection;
        private static bool _deviceConnected;

        static void Main(string[] args)
        {
            StartServer();
            while (true)
            {

            }
        }

        private static async void StartServer()
        {
            _connection = GetConnection();
            if (_connection == null)
                return;
            _deviceConnected = true;

            using (var session = new OttoSession(_connection))
            {
                await session.TestConnectionAsync();

                var listener = new HttpListener();
                listener.Prefixes.Add("http://+:12345/");
                //listener.Prefixes.Add("http://127.0.0.1:12345/");
                listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

                listener.Start();
                Console.WriteLine("HttpListener started");

                while (true)
                {
                    var context = await listener.GetContextAsync();
                    Console.WriteLine($"Request: {context.Request.HttpMethod} {context.Request.RawUrl}");
                    context.Response.AppendHeader("Access-Control-Allow-Origin", "*");

                    if (context.Request.HttpMethod == HttpMethod.Post.Method)
                    {
                        ParseUrl(context.Request.RawUrl, out var resource, out var value);

                        switch (resource)
                        {
                            case "commands":
                                var result = await session.SendStringCommandAsync(value);
                                context.Response.StatusCode = result ? 201 : 400;
                                context.Response.Close();
                                continue;
                            default:
                                context.Response.StatusCode = 404;
                                context.Response.Close();
                                continue;

                        }
                    }
                    if (context.Request.HttpMethod == HttpMethod.Get.Method)
                    {
                        ParseUrl(context.Request.RawUrl, out var resource, out var value);

                        switch (resource)
                        {
                            case "connection":
                                await WriteTextAsync(context, _deviceConnected.ToString());
                                continue;
                            case "distance":
                                var distance = await session.GetStringDataAsync(resource);
                                await WriteTextAsync(context, distance);
                                continue;
                        }
                    }
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }
        }

        private static ISerialConnection GetConnection()
        {
            ISerialConnection connection = null;
            Console.WriteLine("Searching for serial ports");
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports.Where(p => p != "COM3" && p != "COM1"))
            {
                try
                {
                    connection = new EnhancedSerialConnection(port, SerialBaudRate.Bps_57600);
                    Console.WriteLine($"Connected to port {connection.PortName} at {connection.BaudRate} baud.");
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to connect serial port: " + e);
                    connection = null;
                }
            }

            return connection;
        }

        private static void ParseUrl(string url, out string resource, out string value)
        {
            var path = url.TrimStart('/');
            if (path.Contains("/"))
            {
                var values = path.Split('/');
                resource = values[0];
                value = values[1];
                return;
            }
            resource = path;
            value = string.Empty;
        }

        private static async Task WriteTextAsync(HttpListenerContext context, string text)
        {
            Console.WriteLine("Write response: " + text);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }
    }
}
