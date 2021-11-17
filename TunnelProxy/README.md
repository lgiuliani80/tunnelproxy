# Install as a service

1. Publish the project (`dotnet publish` or use click on the '_Publish_' button in Visual Studio).
2. Open an elevated cmd prompt:

    sc create tcpproxytunnel1 start= auto DisplayName= "TCP to HTTP proxy tunnel <listening-port>" binPath= "<full-path-to-publish-folder>\TunnelProxy.exe --listeningPort <listening-port> --proxyHost <http-proxy-host> --proxyPort <http-proxy-port> --destinationHost <destination-of-CONNECT-host> --destinationPort <destination-of-CONNECT-port>"

   Beware to keep the spaces __EXACTLY__ how specified in the line above. Moreover <full-path-to-publish-folder> shall NOT contain spaces.
