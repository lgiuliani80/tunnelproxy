# Running TunnelProxy as a Windows Service

This document provides comprehensive instructions for deploying and managing TunnelProxy as a Windows Service.

## Prerequisites

- Windows operating system (Windows Server 2016+ or Windows 10+)
- .NET 8.0 Runtime or SDK installed
- Administrator privileges to install and manage Windows Services

## Publishing the Application

Before installing TunnelProxy as a Windows Service, you need to publish the application:

### Option 1: Using .NET CLI

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o C:\TunnelProxy\publish
```

**Notes:**
- `-c Release`: Builds in Release mode for optimized performance
- `-r win-x64`: Targets Windows 64-bit (use `win-x86` for 32-bit Windows)
- `--self-contained false`: Requires .NET Runtime to be installed (smaller deployment size)
- `-o C:\TunnelProxy\publish`: Output directory (must NOT contain spaces)

### Option 2: Using Visual Studio

1. Right-click on the TunnelProxy project in Solution Explorer
2. Select **Publish**
3. Choose **Folder** as the target
4. Select a publish folder path without spaces (e.g., `C:\TunnelProxy\publish`)
5. Click **Publish**

## Installing as a Windows Service

### Step 1: Create the Windows Service

Open an **elevated Command Prompt** (Run as Administrator):

```cmd
sc create TcpProxyTunnel8080 start= auto DisplayName= "TCP to HTTP Proxy Tunnel (Port 8080)" binPath= "C:\TunnelProxy\publish\TunnelProxy.exe --listeningPort 8080 --proxyHost proxy.example.com --proxyPort 8080 --destinationHost sftp.external.com --destinationPort 22 --instanceName TcpProxyTunnel8080"
```

**Important Notes:**
- The spaces after `start=`, `DisplayName=`, and `binPath=` are **REQUIRED** by the `sc` command
- The publish folder path must NOT contain spaces
- Replace placeholders with your actual values:
  - `TcpProxyTunnel8080`: Service name (must be unique if running multiple instances)
  - `Port 8080`: Descriptive name for display purposes
  - `8080`: The local port to listen on
  - `proxy.example.com`: Your HTTP proxy server hostname
  - `8080`: Your HTTP proxy server port
  - `sftp.external.com`: The destination host you want to connect to
  - `22`: The destination port
  - `TcpProxyTunnel8080`: Application log source name (for Windows Event Log)

### Step 2: Start the Service

```cmd
sc start TcpProxyTunnel8080
```

Or use Windows Services Manager:
1. Press `Win + R`, type `services.msc`, and press Enter
2. Find "TCP to HTTP Proxy Tunnel (Port 8080)" in the list
3. Right-click and select **Start**

### Step 3: Verify the Service is Running

```cmd
sc query TcpProxyTunnel8080
```

Expected output should show `STATE: 4 RUNNING`

You can also check the Windows Event Log:
1. Open Event Viewer (`eventvwr.msc`)
2. Navigate to **Windows Logs** > **Application**
3. Look for events from source "TcpProxyTunnel8080"

## Managing the Windows Service

### Check Service Status

```cmd
sc query TcpProxyTunnel8080
```

### Stop the Service

```cmd
sc stop TcpProxyTunnel8080
```

### Restart the Service

```cmd
sc stop TcpProxyTunnel8080
sc start TcpProxyTunnel8080
```

### Change Service Configuration

To modify service parameters after installation, you need to delete and recreate the service:

```cmd
sc stop TcpProxyTunnel8080
sc delete TcpProxyTunnel8080
sc create TcpProxyTunnel8080 start= auto DisplayName= "TCP to HTTP Proxy Tunnel (Port 8080)" binPath= "C:\TunnelProxy\publish\TunnelProxy.exe --listeningPort 8080 --proxyHost newproxy.example.com --proxyPort 8080 --destinationHost sftp.external.com --destinationPort 22 --instanceName TcpProxyTunnel8080"
sc start TcpProxyTunnel8080
```

### Uninstall the Service

```cmd
sc stop TcpProxyTunnel8080
sc delete TcpProxyTunnel8080
```

## Running Multiple Instances

TunnelProxy supports running multiple instances simultaneously to tunnel to different destinations or through different proxies. Each instance must:
- Have a unique service name
- Listen on a different port
- Have a unique instance name for logging

**Example: Installing a second instance for a different destination:**

```cmd
sc create TcpProxyTunnel8081 start= auto DisplayName= "TCP to HTTP Proxy Tunnel (Port 8081)" binPath= "C:\TunnelProxy\publish\TunnelProxy.exe --listeningPort 8081 --proxyHost proxy.example.com --proxyPort 8080 --destinationHost database.external.com --destinationPort 3306 --instanceName TcpProxyTunnel8081"
sc start TcpProxyTunnel8081
```

## Configuration Parameters

| Parameter | Required | Description | Example |
|-----------|----------|-------------|---------|
| `--listeningPort` | Yes | Local port to listen on | `8080` |
| `--listeningHost` | No | Local IP to bind to (default: `127.0.0.1`) | `0.0.0.0` |
| `--proxyHost` | Yes | HTTP proxy server hostname | `proxy.example.com` |
| `--proxyPort` | Yes | HTTP proxy server port | `8080` |
| `--destinationHost` | Yes | Final destination hostname | `sftp.external.com` |
| `--destinationPort` | Yes | Final destination port | `22` |
| `--instanceName` | No | Event log source name (default: application name) | `TcpProxyTunnel8080` |

## Troubleshooting

### Service fails to start

1. **Check Windows Event Log** for error messages:
   - Open Event Viewer (`eventvwr.msc`)
   - Navigate to **Windows Logs** > **Application**
   - Look for error events from your instance name

2. **Common Issues:**
   - Port already in use: Choose a different `--listeningPort`
   - Invalid parameters: Verify all required parameters are provided
   - Network connectivity: Ensure the proxy server is reachable
   - Permissions: Ensure the service has appropriate network permissions

### Service starts but connections fail

1. **Verify proxy settings** are correct
2. **Check firewall rules** allow the connection
3. **Test proxy connectivity** from the server:
   ```cmd
   telnet proxy.example.com 8080
   ```
4. **Review Event Log** for connection errors

### Testing the tunnel manually

Before installing as a service, you can test the application interactively:

```cmd
cd C:\TunnelProxy\publish
TunnelProxy.exe --listeningPort 8080 --proxyHost proxy.example.com --proxyPort 8080 --destinationHost sftp.external.com --destinationPort 22
```

Press `Ctrl+C` to stop. If this works correctly, the service installation should work as well.

### Port conflicts

If you get an error about the port being in use:

```cmd
netstat -ano | findstr :<port-number>
```

This will show which process is using the port.

## Using PowerShell Instead of CMD

If you prefer PowerShell, you'll need additional escaping for the `binPath` parameter:

```powershell
sc.exe create TcpProxyTunnel8080 start= auto DisplayName= "TCP to HTTP Proxy Tunnel (Port 8080)" binPath= "C:\TunnelProxy\publish\TunnelProxy.exe --listeningPort 8080 --proxyHost proxy.example.com --proxyPort 8080 --destinationHost sftp.external.com --destinationPort 22 --instanceName TcpProxyTunnel8080"
```

Note: Use `sc.exe` instead of `sc` to avoid conflicts with PowerShell's `Set-Content` alias.

## Security Considerations

- **Run with least privileges**: The service runs under the Local System account by default. Consider using a dedicated service account with minimal permissions.
- **Network security**: Ensure only authorized clients can connect to the listening port
- **Proxy authentication**: Currently, this application does not support proxy authentication
- **Encryption**: The tunnel itself doesn't add encryption; use it with protocols that have built-in encryption (like SFTP, HTTPS)
