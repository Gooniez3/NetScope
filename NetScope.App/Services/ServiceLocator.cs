using NetScope.Core.Diagnostics;
using NetScope.Core.Networking;
using NetScope.Core.Persistence;
using NetScope.Infrastructure.Network;
using NetScope.Infrastructure.Persistence;

namespace NetScope.App.Services;

/// <summary>
/// Simple service locator for wiring Core interfaces to Infrastructure implementations.
/// Provides singleton instances of services used across the application.
/// </summary>
public static class ServiceLocator
{
    private static readonly Lazy<INetworkInterfaceService> _interfaceService =
        new(() => new NetworkInterfaceService());

    private static readonly Lazy<IPingService> _pingService =
        new(() => new PingService());

    private static readonly Lazy<IDnsService> _dnsService =
        new(() => new DnsService());

    private static readonly Lazy<ITracerouteService> _tracerouteService =
        new(() => new TracerouteService());

    private static readonly Lazy<INetworkScannerService> _scannerService =
        new(() => new NetworkScannerService(_interfaceService.Value));

    private static readonly Lazy<INetworkMonitorService> _monitorService =
        new(() => new NetworkMonitorService(_pingService.Value, _dnsService.Value, _interfaceService.Value));

    private static readonly Lazy<SqliteMeasurementRepository> _repository =
        new(() =>
        {
            var repo = new SqliteMeasurementRepository();
            repo.InitializeAsync().GetAwaiter().GetResult();
            return repo;
        });

    private static readonly Lazy<IPortTestService> _portTestService =
        new(() => new PortTestService());

    private static readonly Lazy<IDiagnosticAnalyzer> _diagnosticAnalyzer =
        new(() => new DiagnosticAnalyzer());

    public static INetworkInterfaceService InterfaceService => _interfaceService.Value;
    public static IPingService PingService => _pingService.Value;
    public static IDnsService DnsService => _dnsService.Value;
    public static ITracerouteService TracerouteService => _tracerouteService.Value;
    public static INetworkScannerService ScannerService => _scannerService.Value;
    public static INetworkMonitorService MonitorService => _monitorService.Value;
    public static IMeasurementRepository Repository => _repository.Value;
    public static IPortTestService PortTestService => _portTestService.Value;
    public static IDiagnosticAnalyzer DiagnosticAnalyzer => _diagnosticAnalyzer.Value;
}
