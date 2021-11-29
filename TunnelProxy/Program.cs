using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;

namespace TunnelProxy
{
    class Program
    {
        static readonly string EventSourceName = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product;

        static void Main(string[] args)
        {
            SetupEventSource();

            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hb, sp) =>
                {
                    sp.AddHostedService<TCPProxyAgent>();
                    sp.Configure<EventLogSettings>(settings => { 
                        settings.SourceName = hb.Configuration["instanceName"] ?? hb.HostingEnvironment.ApplicationName; 
                    });
                })
                .UseWindowsService()
                .Build()
                .Run();
        }

        private static void SetupEventSource()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    #pragma warning disable CA1416 // Validate platform compatibility
                    if (!EventLog.SourceExists(EventSourceName))
                    {
                        EventLog.CreateEventSource(EventSourceName, "Application");
                        Console.WriteLine("Event Source created OK!");
                    }
                    #pragma warning restore CA1416 // Validate platform compatibility
                }
                catch { }
            }
        }
    }
}
