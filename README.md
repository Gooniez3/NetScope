# NetScope

**Network visibility. Diagnostics. Intelligence.**

A cross-platform desktop application for monitoring, diagnosing, and understanding local and internet network connectivity.

## Architecture

| Project | Purpose |
|---|---|
| **NetScope.Core** | Models and service interfaces — zero implementation dependencies |
| **NetScope.Infrastructure** | Implementations that hit the OS, network, and external APIs |
| **NetScope.App** | Avalonia UI desktop application (Phase 7) |
| **NetScope.Cli** | Console diagnostic runner for verifying the engine |
| **NetScope.Tests** | xUnit test project |

Built with .NET 10, Avalonia 12, Clean Architecture.

---

## CLI Usage

```
netscope <command> [options]
```

### Commands

| Command | Description |
|---|---|
| `info` | Show network interfaces, connection status, and public IP |
| `ping <target> [options]` | Ping a host and display latency statistics |
| `dns <hostname>` | Resolve a hostname and display addresses |
| `trace <target> [options]` | Traceroute to a host |
| `help` | Show help |

### Ping Options

| Option | Default | Range | Description |
|---|---|---|---|
| `--count`, `-c` | 4 | 1–1,000 | Number of ICMP echo probes |
| `--interval`, `-i` | 1000 | 100–60,000 ms | Delay between probes |
| `--timeout`, `-t` | 3000 | 100–30,000 ms | Timeout per probe |
| `--size`, `-s` | 32 | 0–65,500 bytes | ICMP payload size |
| `--ttl` | OS default | 1–255 | Time to live (hop limit) |

### Examples

```sh
# Network information
dotnet run --project NetScope.Cli -- info

# Basic ping
dotnet run --project NetScope.Cli -- ping 1.1.1.1

# 10 fast probes
dotnet run --project NetScope.Cli -- ping google.com --count 10 --interval 500

# Custom payload and TTL
dotnet run --project NetScope.Cli -- ping 8.8.8.8 -c 20 -s 64 --ttl 64

# DNS lookup
dotnet run --project NetScope.Cli -- dns google.com

# Traceroute
dotnet run --project NetScope.Cli -- trace 1.1.1.1

# Traceroute — skip reverse DNS, limit hops
dotnet run --project NetScope.Cli -- trace google.com --max-hops 20 --no-dns
```

### Traceroute Options

| Option | Default | Range | Description |
|---|---|---|---|
| `--max-hops`, `-m` | 30 | 1–64 | Maximum number of hops |
| `--timeout`, `-t` | 3000 | 100–10,000 ms | Timeout per hop probe |
| `--no-dns` | (off) | — | Skip reverse DNS lookups on hops |

---

## Ping Engine

### Design

The ping engine is built to be reused by multiple consumers: the CLI, the dashboard, live monitoring, historical metrics, alerts, and the AI diagnostic assistant.

- **`IPingService`** — async interface with `IProgress<PingResult>` for real-time per-probe callbacks
- **`PingService`** — implementation using `System.Net.NetworkInformation.Ping`
- **`PingOptions`** — validated configuration with safe upper/lower bounds
- **`PingResult`** — structured outcome for each individual probe
- **`PingStatistics`** — aggregate metrics computed via a pure `Calculate()` factory method

### Statistics Definitions

| Metric | Definition |
|---|---|
| **Sent** | Total number of probes dispatched |
| **Received** | Probes that received a successful ICMP reply |
| **Packet loss %** | `(sent − received) / sent × 100` |
| **Min RTT** | Lowest round-trip time among successful probes |
| **Max RTT** | Highest round-trip time among successful probes |
| **Avg RTT** | Arithmetic mean of successful round-trip times |
| **Jitter** | Mean of absolute differences between consecutive RTTs (see below) |

### Jitter Calculation

NetScope uses the **mean absolute consecutive difference** method, inspired by RFC 3550's interarrival jitter concept:

```
jitter = mean(|RTT[i] − RTT[i−1]|)  for i = 1..N−1
```

- Requires at least 2 successful probes to compute.
- A jitter of **0 ms** means every probe had identical latency.
- Higher values indicate less stable connectivity.
- This is the same approach used by most network monitoring tools and VoIP quality assessments.

### Error Handling

The engine does **not** throw exceptions for expected network failures:

- Timeouts → `PingResult` with `Success = false`, `Status = "TimedOut"`
- Unreachable hosts → structured `PingResult` with descriptive `ErrorMessage`
- DNS resolution failures → `PingResult` with `Status = "PingException"`
- Cancellation (Ctrl+C) → returns results collected so far with computed statistics

Exceptions are reserved for programming errors (null arguments, invalid options).

---

## DNS Engine

### Design

- **`IDnsService`** — async interface returning structured `DnsQueryResult`
- **`DnsService`** — implementation using `System.Net.Dns.GetHostEntryAsync`
- **`DnsQueryResult`** — hostname, resolved addresses (with address family), duration, success/failure
- **`DnsResolvedAddress`** — individual address with `AddressFamily` and human-readable label

### Capabilities

- Resolves hostnames to IPv4 and IPv6 addresses
- Reports resolution duration
- Graceful failure for unknown hosts (structured result, no exception)
- Supports cancellation

---

## Traceroute Engine

### Design

- **`ITracerouteService`** — async interface with `IProgress<TracerouteHop>` for live output
- **`TracerouteService`** — pure .NET implementation using ICMP ping with incrementing TTL
- **`TracerouteOptions`** — validated configuration (max hops, timeout, reverse DNS toggle)
- **`TracerouteHop`** — single hop with address, hostname, RTT, responded status
- **`TracerouteResult`** — complete trace with destination-reached flag, cancellation state

### How it works

Each hop sends an ICMP echo with `TTL = hop number`. The router at that hop returns `TtlExpired`,
revealing its address. When the reply comes from the destination itself with `Success`, the trace
is complete. This is the same algorithm as `tracert`/`traceroute`, but implemented entirely within
.NET — no shell commands, no OS-specific binaries.

### Capabilities

- Configurable maximum hops (1–64)
- Configurable timeout per hop (100–10,000 ms)
- Optional reverse DNS on each hop
- Real-time per-hop progress reporting
- Cancellation support (returns partial results)
- Graceful handling of DNS resolution failure, timeout hops, unreachable destinations

---

### Cross-Platform Notes

| Concern | Behavior |
|---|---|
| **Windows** | Full support. ICMP ping, TTL, payload size all work as expected. |
| **Linux** | Requires `net.ipv4.ping_group_range` sysctl or `CAP_NET_RAW` capability. TTL in replies may report 0 for localhost on some kernels. |
| **macOS** | Full support through .NET runtime. DHCP server queries are disabled (unsupported API). |
| **Payload** | Large payloads may be rejected by intermediate routers or the OS. Surfaced as a non-success result, not an exception. |
| **TTL** | When set to `null`, uses the OS default (typically 128 on Windows, 64 on Linux/macOS). |
| **DNS** | Uses `System.Net.Dns` — cross-platform, resolves via OS resolver (respects /etc/resolv.conf on Linux/macOS). |
| **Traceroute** | Pure .NET ICMP-based implementation. No dependency on `tracert` or `traceroute` binaries. Some routers silently drop TTL-expired ICMP, causing `*` timeout hops — this is normal. |

---

## Development Phases

- [x] **Phase 1** — Network information (interfaces, IPs, gateway, DNS, public IP, connection test)
- [x] **Phase 2** — Ping / latency engine
- [x] **Phase 3** — DNS / traceroute
- [ ] Phase 4 — LAN scanner
- [ ] Phase 5 — Monitoring engine
- [ ] Phase 6 — SQLite history
- [ ] Phase 7 — Desktop UI (Avalonia)
- [ ] Phase 8 — AI diagnostics
- [ ] Phase 9 — Testing, security, packaging
- [ ] Phase 10 — Portfolio presentation

---

## Building

```sh
dotnet build NetScope.slnx
```

## Running Tests

```sh
dotnet test NetScope.slnx
```
