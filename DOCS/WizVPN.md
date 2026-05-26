# WizVPN – Design & Architecture Document

## Overview
WizVPN is a Windows-first secure connectivity platform designed to allow remote developers and support engineers to securely access customer-hosted databases (MSSQL, MySQL, PostgreSQL, etc.) without exposing public database ports.

The system works similarly to TeamViewer or AnyDesk:
- The customer installs the WizVPN Client
- The internal development/support team installs the WizVPN Server
- Secure peer-to-peer VPN tunnels are established through a central broker
- Developers can securely access remote customer databases over encrypted channels

## Core Objectives
- Simple installation
- Always-on background service
- Secure encrypted communications
- NAT traversal support
- Zero database port exposure
- Minimal customer configuration
- Enterprise-grade auditing
- Secure device identity management

## Main Components

### 1. WizVPN Client
Installed on the customer machine/server.

Responsibilities:
- Runs as Windows Service
- Maintains VPN tunnel
- Stores encrypted DB configuration
- Maintains device identity
- Handles remote connection requests
- Provides system tray management

Features:
- Auto-start on boot
- Silent reconnect
- System tray icon
- Right-click menu
- Connection diagnostics
- Secure credential storage
- Pairing support
- Auto-update support

### 2. WizVPN Server
Installed inside developer/support environment.

Responsibilities:
- Manages client directory
- Initiates remote connections
- Maintains secure sessions
- Stores encrypted metadata
- Provides audit logging
- Controls access permissions

Features:
- Multi-client management
- Search/filter clients
- One-click connect
- Connection history
- RBAC support
- Tunnel monitoring
- Route management

### 3. WizVPN Broker
Cloud-hosted coordination server.

Responsibilities:
- Client registration
- NAT traversal
- Session coordination
- Relay fallback
- Public key exchange
- Device discovery

Features:
- Lightweight API
- HTTPS communication
- STUN-like capabilities
- WebSocket coordination
- Session token management

## Recommended Open Source VPN Engine

### Primary Recommendation: WireGuard

Why WireGuard?
- Modern cryptography
- Extremely fast
- Lightweight
- Small codebase
- Easy integration
- Excellent Windows support
- Works well with NAT traversal

Technologies:
- Wintun Driver
- wireguard.exe
- wireguard-go
- UDP-based encrypted tunnels

How WizVPN Uses WireGuard:
WizVPN acts as a management layer around WireGuard.

WizVPN handles:
- Pairing
- Configuration generation
- Tunnel lifecycle
- Device discovery
- Encryption management
- Session routing
- Relay fallback
- Logging
- UI management

WireGuard handles:
- Encryption
- Secure tunnel transport
- Key exchange
- Tunnel networking

## Alternative VPN Engines

### OpenVPN
Useful fallback for restrictive enterprise environments.

Advantages:
- SSL/TLS based
- Mature ecosystem
- Enterprise familiarity

Disadvantages:
- Slower
- Larger footprint
- More complex configuration

### SoftEther
Useful for relay fallback and difficult firewall environments.

Advantages:
- NAT traversal
- VPN Azure relay support
- Multiple protocols

Disadvantages:
- More complex
- Larger attack surface

## Preferred Technology Stack

| Component | Technology |
|---|---|
| Desktop UI | WPF (.NET 8) |
| Windows Service | .NET Worker Service |
| VPN Core | WireGuard |
| Driver | Wintun |
| Encryption | DPAPI + AES |
| IPC | Named Pipes |
| Backend Broker | ASP.NET Core |
| Database | PostgreSQL |
| Logging | Serilog |
| Updates | Squirrel/MSIX |
| Installer | WiX/MSIX |

## Device Identity System

Each client installation generates a permanent device identity.

Requirements:
- Stable
- Non-changing
- Unique
- Secure
- Not hardware-dependent

Recommended approach:
Generate random GUID during first launch and store encrypted via DPAPI.

Example:
WZ-3F89A2B7D1C64A91A7B2E1F4

## AppInfo.log Design

Purpose:
Stores encrypted customer database details.

Location:
%ProgramData%\WizVPN\AppInfo.log

Encryption:
- Windows DPAPI
- AES wrapper
- Machine-scoped encryption

Stored Data:
- Database Type
- Hostname
- Port
- Database Name
- Username
- Password
- Last Updated
- Device ID

## System Tray Features

Context Menu:
- Connect
- Disconnect
- Server Details
- Diagnostics
- Show Device ID
- Logs
- Exit

## First-Time Setup Flow

1. Install WizVPN Client
2. Windows Service starts
3. AppInfo.log checked
4. Setup form shown if missing
5. User enters database details
6. Configuration validated
7. Config encrypted
8. Device registered with broker
9. Ready for remote pairing

## Pairing Flow

Client Side:
User sends Device ID to support team.

Server Side:
1. Developer opens WizVPN Server
2. Clicks Add Connection
3. Enters Device ID
4. Broker coordinates connection
5. Secure tunnel established
6. Metadata synchronized

## Connection Modes

### Mode 1: Port Forwarding
Maps local port to remote DB.

Example:
localhost:1433 -> customer-db:1433

Best for:
- SSMS
- MySQL Workbench
- DBeaver
- pgAdmin

### Mode 2: Routed Subnet
Creates VPN subnet.

Example:
10.20.0.x network

Best for:
- Multiple servers
- Enterprise support

## NAT Traversal

Required because most clients are behind:
- Routers
- Dynamic IPs
- Firewalls
- Carrier NAT

Techniques:
- UDP Hole Punching
- STUN
- Keep-alives
- Relay fallback

## Relay Mode

Purpose:
Fallback when direct P2P fails.

Architecture:
Client -> WizRelay -> Server

Advantages:
- Works behind strict firewalls
- No port forwarding required

Disadvantages:
- Higher latency
- Bandwidth costs

## Security Features

Encryption:
- WireGuard encryption
- TLS 1.3
- DPAPI
- AES-256

Authentication:
- Device certificates
- Public/private keys
- MFA (future)

Security Policies:
- Port restrictions
- IP allowlists
- Time-based access
- Session expiration

## Audit Logging

Logs to Capture:
- Connection attempts
- Successful sessions
- Disconnects
- IP addresses
- User identities
- Session durations

## Automatic Updates

Features:
- Silent updates
- Version rollback
- Channel support
- Signed packages

## Future Features
- Linux client
- macOS client
- Mobile companion app
- Web dashboard
- SSO integration
- Azure AD integration
- Multi-tenant architecture
- SaaS licensing
- Session recording
- File transfer
- Remote shell

## Recommended Development Phases

### Phase 1
- Windows Client
- Windows Service
- Tray App
- AppInfo.log encryption
- WireGuard integration
- Manual pairing

### Phase 2
- Broker server
- NAT traversal
- Relay support
- Tunnel diagnostics

### Phase 3
- Enterprise management
- RBAC
- MFA
- Logging dashboard
- SaaS architecture

## Recommended Folder Structure

WizVPN/
├── WizVPN.Client.Tray/
├── WizVPN.Client.Service/
├── WizVPN.Server/
├── WizVPN.Broker/
├── WizVPN.Shared/
├── WizVPN.WireGuard/
├── WizVPN.Security/
├── WizVPN.Installer/
└── Docs/

## Recommended Database Tables

### Clients
- ClientID
- DeviceID
- CustomerName
- LastSeen
- PublicKey
- Status

### Connections
- ConnectionID
- ClientID
- CreatedBy
- ConnectedAt
- DisconnectedAt

### AuditLogs
- AuditID
- EventType
- User
- Timestamp
- Details

## Why C#/.NET is Excellent for WizVPN

Advantages:
- Excellent Windows integration
- Strong service support
- WPF desktop support
- Easy installer creation
- Great IPC support
- Strong cryptography APIs
- DPAPI integration
- Strong async support

Recommended Version:
.NET 8

## Recommended UI Style

Design Goals:
- Minimalistic
- Enterprise-grade
- Low user interaction
- Always accessible
- Clean diagnostics

UI Style:
- Dark mode
- Fluent UI
- Tray-first experience
- Simple onboarding

## Conclusion

WizVPN should not reinvent VPN encryption.

Instead:
- Use WireGuard/OpenVPN as the secure tunnel engine
- Build a polished orchestration layer around it
- Focus on:
  - usability
  - pairing
  - NAT traversal
  - diagnostics
  - auditing
  - enterprise management

This approach dramatically reduces:
- development time
- security risk
- maintenance burden

while producing a highly professional enterprise-ready product.
