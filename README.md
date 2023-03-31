# TCP to HTTP proxy tunneler

This server listens on a (local) port and, when a connection is received, opens a connection to the configured HTTP proxy server, 
issues the '`CONNECT`' request towards the configured destination host, then establishes a transparent tunnel between the local client and the remote server.

This is useful when having to deal with *services* (like e.g. SFTP connector for Data Factory running on a self-hosted Integration Runtime) *which __do not__ support proxy server* configuration
and the (enterprise) network in which they're running require the use of a proxy to access the Internet.

NOTES:
1. each instance of this server will tunnel all clients to the same configured server through the same HTTP proxy. Multiple instances of the server can run in parallel though
   (it's advised to pass all the configuration via command line or via environment variables, and not via appsettings.json, as this allows for the same executable to run in 
    multiple parallel instances with different configurations).
2. this server supports HTTP proxies ONLY, no support to SOCKS4/5 proxies.
3. connection to proxies is not recycled nor pooled, so it's not intended for high rate of incoming connections to serve.
4. the code is written in .NET 5 so it's platform indipendent. On Windows though it's intended to be run as Windows Service. Look at [this](TunnelProxy/README.md) README for details on how to create
   system service.
