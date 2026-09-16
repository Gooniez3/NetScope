# Engine and CLI reference

Detailed networking engines, CLI flags, persistence, and the rule-based analyzer. The product overview lives in the [root README](../README.md). Architecture is in [ARCHITECTURE.md](ARCHITECTURE.md).

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
| `scan [options]` | Scan the local network for devices |
| `monitor [options]` | Continuous network health monitoring |
| `history [options]` | View saved monitoring sessions and measurements |
| `stats [options]` | Show aggregate statistics from saved data |
| `analyze [options]` | Explain saved measurements (rule-based diagnostics) |
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

# LAN scan — auto-detect subnet
dotnet run --project NetScope.Cli -- scan

# LAN scan — explicit subnet, high concurrency, no DNS
dotnet run --project NetScope.Cli -- scan --subnet 192.168.1.0/24 --concurrency 64 --no-dns

# Monitor — 5 cycles against 1.1.1.1
dotnet run --project NetScope.Cli -- monitor --target 1.1.1.1 --count 5

# Monitor — 10-second interval, include DNS timing
dotnet run --project NetScope.Cli -- monitor --interval 10 --dns --count 10

# Monitor with persistence
dotnet run --project NetScope.Cli -- monitor --save --count 5

# Monitor — indefinite until Ctrl+C
dotnet run --project NetScope.Cli -- monitor --target 8.8.8.8

# View saved sessions
dotnet run --project NetScope.Cli -- history

# Session statistics
dotnet run --project NetScope.Cli -- stats --session 1

# Explain a saved session
dotnet run --project NetScope.Cli -- analyze --session 1
```

### Scan Options

| Option | Default | Range | Description |
|---|---|---|---|
| `--subnet`, `-s` | Auto-detect | CIDR `/16`–`/30` | Subnet to scan (e.g. `192.168.1.0/24`). Prefixes wider than `/16` are rejected. |
| `--timeout`, `-t` | 500 | 100–10,000 ms | Timeout per ping probe |
| `--concurrency`, `-c` | 32 | 1–256 | Max concurrent probes |
| `--no-dns` | (off) | — | Skip reverse DNS lookups |
| `--no-mac` | (off) | — | Skip MAC address discovery |

### Traceroute Options

| Option | Default | Range | Description |
|---|---|---|---|
| `--max-hops`, `-m` | 30 | 1–64 | Maximum number of hops |
| `--timeout`, `-t` | 3000 | 100–10,000 ms | Timeout per hop probe |
| `--no-dns` | (off) | — | Skip reverse DNS lookups on hops |

---

## Ping Engine

### Design

The ping engine is built to be reused by multiple consumers: the CLI, the dashboard, live monitoring, historical metrics, alerts, and the diagnostic analyzer.

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

---

## LAN Scanner

### Design

- **`INetworkScannerService`** — async interface with `IProgress<DiscoveredDevice>` for live device reporting
- **`NetworkScannerService`** — concurrent ICMP ping sweep with `SemaphoreSlim` throttling
- **`SubnetHelper`** — pure math utility for CIDR parsing, subnet masks, host enumeration (fully testable, no I/O)
- **`ArpTableReader`** — platform-specific ARP table reader (Windows `arp -a`, Linux `/proc/net/arp`, macOS `arp -an`)
- **`ScanOptions`** — validated configuration (subnet, timeout, concurrency, DNS/MAC toggles)
- **`DiscoveredDevice`** — IP, hostname, MAC, response time, discovery method, status
- **`NetworkScanResult`** — scanned range, duration, device count, cancellation state

### How it works

1. Resolves the target subnet — either from an explicit CIDR or auto-detected from the active network interface
2. Enumerates all host addresses in the subnet (e.g. .1–.254 for a /24)
3. Sends concurrent ICMP pings with `SemaphoreSlim` throttling
4. Enriches responding hosts with MAC addresses from the OS ARP table and optional reverse DNS
5. Returns results ordered by IP address

### MAC Address Discovery

MAC addresses are obtained from the OS ARP cache, which is populated by network traffic. This means:

- The local machine's own MAC will typically **not** appear (it's not in its own ARP table)
- Devices that haven't communicated recently may not have ARP entries
- MAC visibility depends on being on the same Layer 2 segment
- ARP table reading is platform-specific and isolated in `ArpTableReader` (Infrastructure layer)

| Platform | Method |
|---|---|
| Windows | Parses `arp -a` output |
| Linux | Reads `/proc/net/arp` |
| macOS | Parses `arp -an` output |

---

## Monitoring Engine

### Design

The monitoring engine provides continuous, periodic network health measurement. It is designed to be consumed by any caller: CLI, dashboard, live charts, alerts, database storage, or the diagnostic analyzer.

- **`INetworkMonitorService`** — async streaming interface yielding `IAsyncEnumerable<NetworkMeasurement>`
- **`NetworkMonitorService`** — composes `IPingService` and optionally `IDnsService` via dependency injection
- **`MonitorOptions`** — validated configuration (target, interval, probes, timeout, DNS/gateway toggles, count)
- **`NetworkMeasurement`** — one monitoring sample with connectivity, packet stats, latency, jitter, optional DNS/gateway
- **`NetworkHealthStatus`** — enum (Healthy, Degraded, Unstable, Disconnected) with deterministic classification
- **`HealthClassifier`** — pure static `Classify()` method — no I/O, fully unit-testable

### Monitor Options

| Option | Default | Range | Description |
|---|---|---|---|
| `--target` | `1.1.1.1` | — | Host/IP to monitor |
| `--interval`, `-i` | 5 | 1–300 seconds | Seconds between measurement cycles |
| `--timeout`, `-t` | 3000 | 100–30,000 ms | Timeout per ping probe |
| `--probes`, `-p` | 4 | 1–20 | Probes per measurement cycle |
| `--count`, `-c` | unlimited | 1–100,000 | Number of cycles (omit for indefinite) |
| `--dns` | off | — | Include DNS resolution timing |
| `--gateway` | off | — | Include gateway latency (auto-detect or specify address) |
| `--save` | off | — | Persist measurements to SQLite database |

### Health Classification

Health status is classified from measurable metrics. These are **practical defaults**, not universal network standards.

| Status | Criteria |
|---|---|
| **Healthy** | Loss = 0%, Avg RTT < 100 ms, Jitter < 10 ms |
| **Degraded** | Loss < 10%, or Avg RTT 100–500 ms, or Jitter 10–50 ms |
| **Unstable** | Loss ≥ 10%, or Avg RTT > 500 ms, or Jitter > 50 ms |
| **Disconnected** | Loss = 100% (no probes succeeded) |

The worst matching state wins. The classification is deterministic and implemented in `HealthClassifier.Classify()`, which is a pure function with no I/O.

### How it works

1. Validates options before the first cycle
2. Each cycle runs a short ping session (N probes, 200ms intra-probe interval)
3. Optionally measures DNS resolution time and gateway latency
4. Classifies health from the ping statistics
5. Yields a `NetworkMeasurement` to the caller
6. Waits for the remaining interval (skips wait if the cycle exceeded the interval)
7. Repeats until cancellation or max cycles

### No overlapping cycles

If a measurement cycle takes longer than the configured interval, the next cycle starts immediately — but never concurrently. This guarantees predictable, sequential measurement behavior.

### Cancellation

- **Ctrl+C in CLI** — triggers `CancellationToken`, monitor stops cleanly after the current cycle
- **`MaxCycles` reached** — monitor yields the final measurement and exits
- **Programmatic cancellation** — any consumer can cancel via the `CancellationToken`

### Failure recovery

Individual cycle failures (e.g. all probes timeout) produce a `NetworkMeasurement` with `IsConnected = false` and `HealthStatus = Disconnected`. The monitor continues to the next cycle — it does not throw or stop on transient failures.

### Sample CLI output

```
TIME       TARGET          LATENCY    LOSS     JITTER    STATUS
─────────  ──────────────  ─────────  ───────  ────────  ────────────
10:20:01   1.1.1.1            5.8 ms     0%       1.2 ms  Healthy
10:20:06   1.1.1.1            6.1 ms     0%       1.5 ms  Healthy
10:20:11   1.1.1.1           85.0 ms     0%      22.0 ms  Degraded
```

### Known limitations

- Health classification thresholds are currently hard-coded defaults. Future versions may make them configurable per-session.
- Gateway auto-detection depends on the active interface reporting a gateway address.
- DNS measurement timing may vary with OS resolver caching behavior.
- DNS measurement timing may vary with OS resolver caching behavior on first lookup.

---

## SQLite Persistence

### Design

Monitoring measurements can be persisted to a local SQLite database for historical analysis. Persistence is **opt-in** via the `--save` flag — existing monitoring behavior is unchanged without it.

- **`IMeasurementRepository`** — Core interface (`IAsyncDisposable`) for saving/querying measurements and sessions
- **`SqliteMeasurementRepository`** — Infrastructure implementation using raw `Microsoft.Data.Sqlite` ADO.NET
- **`MonitoringSession`** — Core model representing a monitoring session with config and completion status
- **`MeasurementAggregate`** — Core model for computed aggregate statistics

### Database

| Property | Value |
|---|---|
| **Location** | `~/.netscope/netscope.db` |
| **Engine** | SQLite via `Microsoft.Data.Sqlite` |
| **Journal mode** | WAL (better concurrent read performance) |
| **Schema** | Auto-created on first use |
| **Queries** | All parameterized (SQL injection safe) |

### Schema

```sql
sessions (
    id, started_at, ended_at, target, interval_sec,
    probes, timeout_ms, measurement_cnt, completed
)

measurements (
    id, session_id, timestamp, cycle_number, target, is_connected,
    packets_sent, packets_recv, loss_pct,
    min_latency, max_latency, avg_latency, jitter,
    dns_ms, gateway_ms, health_status
)
```

Indexes on `session_id` and `timestamp` for efficient queries.

### Monitoring Sessions

Each `--save` monitoring run creates a session record that tracks:
- Start/end timestamps
- Target and configuration (interval, probes, timeout)
- Measurement count
- Completion status (normal vs. cancelled/interrupted)

Interrupted sessions (Ctrl+C) are safely marked as cancelled with the measurements collected so far.

### History Command

```sh
# List recent sessions
netscope history

# View measurements for a session
netscope history --session 1

# Recent measurements across all sessions
netscope history --recent --limit 10

# Cleanup old data (default: 30 days)
netscope history --cleanup --older-than 7
```

### Stats Command

```sh
# Aggregate stats for last 24 hours
netscope stats

# Stats for a specific session
netscope stats --session 1

# Stats for a custom time range
netscope stats --hours 48
```

Stats include: measurement count, uptime %, latency (avg/min/max), jitter, packet loss, and health status breakdown.

### Retention

The `--cleanup` flag on the history command deletes measurements older than a specified number of days and removes empty sessions. This is manual — no automatic background cleanup.

```sh
netscope history --cleanup --older-than 30
```

### Persistence limitations

- Persistence is opt-in (`--save`). Without it, measurements are not stored.
- The database location (`~/.netscope/netscope.db`) is not yet configurable via CLI flag.
- No automatic retention/cleanup — must be run manually.
- Single-writer design — concurrent monitoring sessions writing to the same database may cause contention (WAL mode mitigates reads).
- Aggregate statistics are computed in-memory from fetched measurements, not via SQL aggregation.

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
| **LAN Scan** | ICMP ping sweep works cross-platform. MAC discovery via ARP is platform-specific (Windows/Linux/macOS) and isolated in Infrastructure. Returns null MAC on unsupported platforms. |
| **ARP Table** | Windows: `arp -a`; Linux: `/proc/net/arp`; macOS: `arp -an`. Own machine's MAC is not in its own ARP table. Devices must have communicated recently to appear. |
| **SQLite** | Uses `Microsoft.Data.Sqlite` with bundled native SQLite. Works on all platforms without external dependencies. Database stored in `~/.netscope/netscope.db`. |

---

## Diagnostic Analyzer

### Design

The analyzer turns raw monitoring numbers into an explanation a person can act on. It is a **rule-based engine in Core** — deterministic, offline, and fully unit-tested. It does not call an external language-model API.

Instead of only:

```
Packet loss: 8.4%
```

it reports:

```
Possible network instability detected.
Packet loss increased from 0.2% to 8.4% during the last 10 minutes.
Average latency rose from 12.0 ms to 90.0 ms.
Gateway latency is elevated (55.0 ms). The issue appears closer to the local network than the destination.
```

Then it suggests **Run Diagnostics** (ping, DNS, traceroute) when the report is not healthy.

- **`IDiagnosticAnalyzer`** — Core interface, no I/O
- **`DiagnosticAnalyzer`** — pure rule engine (same layer as `HealthClassifier`)
- **`DiagnosticReport`** — headline, summary paragraph, findings, suggested actions
- **`DiagnosticFinding`** — stable code, title, detail, severity, category
- **`DiagnosticAction`** — `RunDiagnostics`, `CheckLocalNetwork`, `ReviewHistory`

### How it works

1. Sorts measurements by timestamp
2. Splits the series in half to compare earlier vs later windows (trends need ≥ 8 samples spanning at least 2 minutes)
3. Detects packet-loss, latency, and jitter trends against fixed deltas
4. Classifies snapshot severity (elevated loss/latency/jitter, outages, intermittent drops)
5. Uses gateway RTT (when present) and optional traceroute hops to decide **local vs upstream**
6. Emits suggested actions the UI can bind (e.g. open Diagnostics with the analyzed target)

### Locality

| Evidence | Conclusion |
|---|---|
| Gateway RTT ≥ 50 ms while the destination is unhealthy | Closer to the local network |
| Gateway RTT < 20 ms while the destination is unhealthy | Closer to the destination or upstream path |
| First traceroute hop times out | Local adapter, link, or gateway |
| Trace leaves the LAN but does not reach the destination | Upstream path |

### CLI

```sh
# Explain a saved session
dotnet run --project NetScope.Cli -- analyze --session 1

# Explain the last 6 hours
dotnet run --project NetScope.Cli -- analyze --hours 6
```

| Option | Default | Description |
|---|---|---|
| `--session`, `-s` | (off) | Analyze a specific monitoring session |
| `--hours`, `-h` | 24 | Time range when `--session` is omitted |

### Why not an API model for v1

The original direction listed an optional API-based assistant. The engine that produces the explanation is local so that:

- Results are reproducible in tests
- Analysis works offline and without API keys
- The UI stays an engineering tool, not a chat panel

A remote narrator can wrap `DiagnosticReport` later without changing the analyzer.

---

## Desktop Application (Avalonia UI)

### Running

```sh
dotnet run --project NetScope.App
```

The application opens a dark-themed professional desktop window with sidebar navigation.

### Pages

| Page | Description |
|---|---|
| **Dashboard** | Connection status, active interface, IP/gateway, health summary, live latency chart, recent measurements |
| **Monitor** | Configurable monitoring (target, interval, probes, timeout), start/stop, save-to-DB, live metrics, latency chart, measurement table |
| **Diagnostics** | Ping, DNS, and Traceroute tools |
| **LAN Discovery** | LAN scan (same Diagnostics engine, LAN Scan tab) |
| **Analysis** | Explain saved sessions or recent hours; findings; Run Diagnostics |
| **History** | Browse saved SQLite sessions, session measurements, aggregate statistics, cleanup |
| **Settings** | Monitoring defaults, database path, about information |

### Architecture

```
NetScope.App/
├── Services/
│   └── ServiceLocator.cs        — wires Core interfaces → Infrastructure implementations
├── ViewModels/
│   ├── ViewModelBase.cs          — ObservableObject base
│   ├── MainViewModel.cs          — sidebar navigation, page switching
│   ├── DashboardViewModel.cs     — network info, quick monitor, live metrics
│   ├── MonitorViewModel.cs       — configurable monitoring, persistence, latency history
│   ├── HistoryViewModel.cs       — session browsing, aggregates, cleanup
│   ├── DiagnosticsViewModel.cs   — ping, DNS, traceroute, LAN scan
│   ├── AnalysisViewModel.cs      — session/time-range analysis, Run Diagnostics
│   └── SettingsViewModel.cs      — defaults and about
├── Views/
│   ├── MainWindow.axaml          — sidebar + content shell
│   ├── DashboardView.axaml       — dashboard page
│   ├── MonitorView.axaml         — monitoring page
│   ├── HistoryView.axaml         — history page
│   ├── DiagnosticsView.axaml     — diagnostics tabs
│   ├── AnalysisView.axaml        — diagnostic analysis page
│   └── SettingsView.axaml        — settings page
├── Styles/
│   └── AppStyles.axaml           — cards, metrics, navigation, typography
├── ViewLocator.cs                — ViewModel → View resolution
├── App.axaml                     — theme (dark), styles, DataGrid
└── Program.cs                    — entry point
```

### Design Principles

- **MVVM** with CommunityToolkit.Mvvm source generators
- **No networking logic in the UI** — all operations go through Core interfaces
- **Async everywhere** — no UI thread blocking
- **Cancellation support** — all long-running operations support stop/cancel
- **Graceful error handling** — network/DB errors shown in status text, never crash
- **Reuses all existing services** — `IPingService`, `IDnsService`, `ITracerouteService`, `INetworkScannerService`, `INetworkMonitorService`, `IMeasurementRepository`, `IDiagnosticAnalyzer`

### UI limitations

- Settings are in-memory only (not persisted to a config file)
- Theme is dark-only (no light theme toggle)
- No system tray or background monitoring when the window is closed

---
