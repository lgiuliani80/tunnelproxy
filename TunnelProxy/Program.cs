using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TunnelProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    //logging.AddEventLog();
                })
                .ConfigureServices(sp =>
                {
                    sp.AddHostedService<TCPProxyAgent>();
                })
                .Build()
                .Run();
        }
    }
}
