# TcpSharp
This clearly states that the TcpSharp fork from [burakoner](https://github.com/burakoner) has been enhanced to support direct IP address usage, without needing to resolve hostnames through DNS.

Original Project: https://github.com/burakoner/TcpSharp
## Features

- Direct IP address support for client

## Usage

### Setting the Host

You can now set the host to either `localhost`,`127.0.0.1` or others:

```csharp
_client.Host = "localhost";
_client.Host = "127.0.0.1";
_client.Host = "10.100.110.11";
