using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace TunnelProxy
{
    public class TCPProxyAgent : IHostedService
    {
        const int PROXY_TIMEOUT_MS = 10000;
        const int MAX_CLIENTS = 10;

        protected readonly ConcurrentDictionary<string, (TcpClient client, TcpClient proxy)> _clients = new();
        protected volatile int _connectionCount;

        protected Thread _serverThread;
        protected ILogger<TCPProxyAgent> _logger;
        protected IHostApplicationLifetime _lifecycle;
        protected IConfiguration _config;

        protected readonly TCPProxyServerConfig _cfg = new TCPProxyServerConfig();

        public TCPProxyAgent(ILogger<TCPProxyAgent> logger, IHostApplicationLifetime lifecycle, IConfiguration configuration)
        {
            _logger = logger;
            _lifecycle = lifecycle;
            _config = configuration;
        }

        private void ManageClientConnection(TcpClient scli)
        {
            TcpClient proxycli = null;
            string clientSpec = scli.Client.RemoteEndPoint.ToString();

            Interlocked.Increment(ref _connectionCount);

            try
            {
                var nscli = scli.GetStream();

                _logger.LogInformation($"[{clientSpec}] Connection established with client: clients connected = {_connectionCount}");

                proxycli = new TcpClient(_cfg.proxyHost, _cfg.proxyPort);
                var ns = proxycli.GetStream();

                _clients[clientSpec] = (scli, proxycli);
                    
                var proxySpec = proxycli.Client.RemoteEndPoint.ToString();

                var msg = $"CONNECT {_cfg.destinationHost}:{_cfg.destinationPort} HTTP/1.1\r\nConnection: close\r\n\r\n";

                _logger.LogDebug($"[{clientSpec}] > {msg}");

                ns.Write(Encoding.ASCII.GetBytes(msg));

                const string EXPECT = "\r\n\r\n";
                var bufin = new byte[1024];
                var established = false;
                var aborted = false;
                var cnt = 0;
                var offset = 0;
                ns.ReadTimeout = PROXY_TIMEOUT_MS;
                var nread = ns.Read(bufin, offset, bufin.Length - offset);

                while (nread > 0)
                {
                    for (int i = 0; i < nread; i++)
                    {
                        //_logger.LogTrace($"[{clientSpec}] < {(char)bufin[offset + i]}");

                        if (bufin[offset + i] == EXPECT[cnt])
                        {
                            cnt++;
                            if (cnt == EXPECT.Length)
                            {
                                var responseStatusAndHeaders = Encoding.UTF8.GetString(bufin, 0, offset + i);
                                var responseLines = responseStatusAndHeaders.Split("\r\n");

                                _logger.LogDebug($"[{clientSpec}] << {responseStatusAndHeaders}");

                                if (!Regex.IsMatch(responseLines[0], @"^HTTP/1\.1 200 .*$"))
                                {
                                    aborted = true;
                                    break;
                                }

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

                    if (established || aborted)
                        break;

                    offset += nread;
                    nread = ns.Read(bufin, offset, bufin.Length - offset);
                }

                if (!established)
                {
                    _logger.LogWarning($"[{clientSpec}] Proxy CONNECT {_cfg.destinationHost}:{_cfg.destinationPort} failed!");
                    return;
                }

                _logger.LogInformation($"[{clientSpec}] Tunnel established");

                ns.ReadTimeout = Timeout.Infinite;

                Task.Run(() =>
                {
                    _logger.LogDebug($"[{clientSpec}] Data tunnelling [proxy({proxySpec})] -> [client({clientSpec})] STARTED");

                    try
                    {
                        int nread2 = 0;
                        var bufin2 = new byte[1024];
                        while ((nread2 = ns.Read(bufin2, 0, bufin2.Length)) > 0)
                        {
                            nscli.Write(bufin2, 0, nread2);
                        }
                    }
                    catch (System.IO.IOException ioe) when ((ioe.InnerException as SocketException)?.SocketErrorCode == SocketError.ConnectionAborted)
                    {
                        _logger.LogInformation($"[{clientSpec}] disconnected");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"[{clientSpec}] Exception in tunnelling proxy -> client");
                        scli.Close();
                    }

                    _logger.LogDebug($"[{clientSpec}] Data tunnelling [proxy({proxySpec})] -> [client({clientSpec})] terminated");
                });

                _logger.LogDebug($"[{clientSpec}] Data tunnelling [client({clientSpec})] -> [proxy({proxySpec})] STARTED");

                try
                {
                    while ((nread = nscli.Read(bufin, 0, bufin.Length)) > 0)
                    {
                        ns.Write(bufin, 0, nread);
                    }
                }
                catch (System.IO.IOException ioe) when ((ioe.InnerException as SocketException)?.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    _logger.LogInformation($"[{clientSpec}] disconnected");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"[{clientSpec}] Exception in tunnelling client -> proxy");
                }

                _logger.LogDebug($"[{clientSpec}] Data tunnelling [client({clientSpec})] -> [proxy({proxySpec})] terminated");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while connecting to the proxy server or managing the tunnel");
            }
            finally
            {
                scli.Close();
                proxycli.Close();
                _clients.Remove(clientSpec, out var removed);
                Interlocked.Decrement(ref _connectionCount);
                _logger.LogInformation($"[{clientSpec}] Tunnel destroyed: clients connected = {_connectionCount}");
            }
        }

        private void ThreadServerProc()
        {
            _logger.LogInformation("Server thread started");

            try
            {
                TcpListener tcpListener = new TcpListener(IPAddress.Parse(_cfg.listeningHost), _cfg.listeningPort);
                TcpClient scli = null;

                tcpListener.Start();

                _lifecycle.ApplicationStopping.Register(() =>
                {
                    try
                    {
                        tcpListener?.Stop();

                        foreach (var v in _clients.Values)
                        {
                            var (scli, proxycli) = v;

                            scli?.Close();
                            proxycli?.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception while stopping the server");
                    }
                });

                _logger.LogDebug("Waiting for a client to connect...");

                while ((scli = tcpListener.AcceptTcpClient()) != null)
                {
                    if (_connectionCount >= MAX_CLIENTS)
                    {
                        try { scli?.Close(); } catch { }
                    }
                    else
                    {
                        Task.Run(() => ManageClientConnection(scli));
                    }

                    _logger.LogDebug("Waiting for a client to connect...");
                }
            } 
            catch (Exception ex)
            {
                if (!_lifecycle.ApplicationStopping.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Unexpected error in main server thread body");
                }
            }

            _lifecycle.StopApplication();
            _serverThread = null;

            _logger.LogInformation("Server thread terminated.");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_serverThread != null)
                return Task.CompletedTask;

            #region Read and validate configuration
            _cfg.listeningPort = _config.GetValue<int>(nameof(TCPProxyServerConfig.listeningPort));
            _cfg.listeningHost = _config.GetValue<string>(nameof(TCPProxyServerConfig.listeningHost), "127.0.0.1");
            _cfg.proxyPort = _config.GetValue<int>(nameof(TCPProxyServerConfig.proxyPort));
            _cfg.proxyHost = _config.GetValue<string>(nameof(TCPProxyServerConfig.proxyHost));
            _cfg.destinationPort = _config.GetValue<int>(nameof(TCPProxyServerConfig.destinationPort));
            _cfg.destinationHost = _config.GetValue<string>(nameof(TCPProxyServerConfig.destinationHost));

            if (_cfg.listeningPort <= 0)
                throw new ArgumentException($"{nameof(TCPProxyServerConfig.listeningPort)} must be > 0");

            if (string.IsNullOrWhiteSpace(_cfg.listeningHost))
                throw new ArgumentException($"{nameof(TCPProxyServerConfig.listeningHost)} must be not empty");

            if (_cfg.proxyPort <= 0)
                throw new ArgumentException($"{nameof(TCPProxyServerConfig.proxyPort)} must be > 0");

            if (string.IsNullOrWhiteSpace(_cfg.proxyHost))
                throw new ArgumentException($"{nameof(TCPProxyServerConfig.proxyHost)} must be not empty");

            if (_cfg.proxyPort <= 0)
                throw new ArgumentException($"{nameof(TCPProxyServerConfig.proxyPort)} must be > 0");
            
            if (string.IsNullOrWhiteSpace(_cfg.destinationHost))
                throw new ArgumentException($"{nameof(TCPProxyServerConfig.destinationHost)} must be not empty");
            #endregion

            _serverThread = new Thread(ThreadServerProc) { Name = "ThreadServerProc" };
            _serverThread.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public class TCPProxyServerConfig
    {
        public string listeningHost = "localhost";
        public int listeningPort;
        public string proxyHost;
        public int proxyPort;
        public string destinationHost;
        public int destinationPort;
    }
}
