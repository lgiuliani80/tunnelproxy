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
            Console.WriteLine("Listening...");

            TcpListener tcpListener = new TcpListener(IPAddress.Any, int.Parse(args[0]));

            tcpListener.Start();

            TcpClient scli;
            TcpClient proxycli;

            while ( (scli =  tcpListener.AcceptTcpClient()) != null )
            {
                var nscli = scli.GetStream();

                proxycli = new TcpClient(args[1], int.Parse(args[2]));
                var ns = proxycli.GetStream();

                var msg = $"CONNECT {args[3]}:{args[4]} HTTP/1.1\r\n\r\n";
                Console.WriteLine(">" + msg);
                ns.Write(System.Text.Encoding.ASCII.GetBytes(msg));

                const string EXPECT = "\r\n\r\n";
                var bufin = new byte[1024];
                var established = false;
                var cnt = 0;
                var offset = 0;
                var nread = ns.Read(bufin, offset, bufin.Length - offset);
                while (nread > 0)
                {
                    for (int i = 0; i < nread; i++)
                    {
                        Console.Write((char)bufin[offset + i]);
                        if (bufin[offset + i] == EXPECT[cnt])
                        {
                            cnt++;
                            if (cnt == EXPECT.Length)
                            {
                                nscli.Write(bufin, offset + i + 1, nread - i - 1);
                                established = true;
                                break;
                            }
                        }
                        else
                        {
                            cnt = 0;
                        }
                    }

                    if (established)
                        break;

                    offset += nread;
                    nread = ns.Read(bufin, offset, bufin.Length - offset);
                }

                if (!established)
                {
                    proxycli.Close();
                    scli.Close();
                    continue;
                }

                Task.Run(() =>
                {
                    int nread2 = 0;
                    var bufin2 = new byte[1024];
                    while ((nread2 = ns.Read(bufin2, 0, bufin2.Length)) > 0)
                    {
                        nscli.Write(bufin2, 0, nread2);
                    }

                });

                while ( (nread = nscli.Read(bufin, 0, bufin.Length)) > 0 )
                {
                    ns.Write(bufin, 0, nread);
                }
            }
        }
    }
}
