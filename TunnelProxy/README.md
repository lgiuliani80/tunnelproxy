# Install as a service

1. Publish the project (`dotnet publish` or use click on the '_Publish_' button in Visual Studio).
2. Open an elevated `cmd` prompt (if using _Powershell_ you'll need various extra escaping):

   ```
   sc create tcpproxytunnel<listening-port> start= auto DisplayName= "TCP to HTTP proxy tunnel <listening-port>" binPath= "<full-path-to-publish-folder>\TunnelProxy.exe --listeningPort <listening-port> --proxyHost <http-proxy-host> --proxyPort <http-proxy-port> --destinationHost <destination-of-CONNECT-host> --destinationPort <destination-of-CONNECT-port> --instanceName <applicationLogSourceName>"
   ```

   Beware to keep the spaces __EXACTLY__ how specified in the line above. Moreover `<full-path-to-publish-folder>` shall NOT contain spaces.

   If more destination external hosts and/or ports are to be supported at the same time, simply install more instances of this service, listening on different ports.
