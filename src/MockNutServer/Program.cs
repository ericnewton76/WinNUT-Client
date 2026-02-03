using System.Net;
using System.Net.Sockets;
using System.Text;
using NLog;

namespace MockNutServer;

class Program
{
    private static readonly Dictionary<string, UpsDevice> _devices = new();
    private static bool _running = true;
    private static readonly object _lock = new();
    
    // ClientServerLog for all NUT protocol interactions - outputs to OutputDebugString
    private static readonly Logger ClientServerLog = LogManager.GetLogger("ClientServerLog");

    static async Task Main(string[] args)
    {
        int port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 3493;

        // Create default UPS devices
        _devices["ups"] = new UpsDevice("ups", "APC", "Smart-UPS 1500");
        _devices["ups2"] = new UpsDevice("ups2", "CyberPower", "CP1500AVRLCD");

        Console.WriteLine($"Mock NUT Server starting on port {port}...");
        Console.WriteLine("Default devices: ups, ups2");
        Console.WriteLine();
        PrintHelp();

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Listening on port {port}");

        // Start console input handler
        _ = Task.Run(HandleConsoleInput);

        while (_running)
        {
            try
            {
                if (listener.Pending())
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(client));
                }
                else
                {
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        listener.Stop();
    }

    static void PrintHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  status                    - Show all UPS devices and their status");
        Console.WriteLine("  set <ups> <var> <value>   - Set a variable (e.g., set ups battery.charge 50)");
        Console.WriteLine("  status <ups> <status>     - Set UPS status (OL, OB, LB, FSD, etc.)");
        Console.WriteLine("  add <ups> <mfr> <model>   - Add a new UPS device");
        Console.WriteLine("  remove <ups>              - Remove a UPS device");
        Console.WriteLine("  discharge <ups>           - Simulate battery discharge (OB + decreasing charge)");
        Console.WriteLine("  restore <ups>             - Restore to online status (OL + 100% charge)");
        Console.WriteLine("  critical <ups>            - Set to critical state (OB LB + 10% charge)");
        Console.WriteLine("  fsd <ups>                 - Force shutdown signal");
        Console.WriteLine("  help                      - Show this help");
        Console.WriteLine("  quit                      - Exit server");
        Console.WriteLine();
    }

    static async Task HandleConsoleInput()
    {
        while (_running)
        {
            var line = await Task.Run(() => Console.ReadLine());
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLower();

            lock (_lock)
            {
                try
                {
                    switch (cmd)
                    {
                        case "status" when parts.Length == 1:
                            foreach (var (name, ups) in _devices)
                            {
                                Console.WriteLine($"  {name}: {ups.Manufacturer} {ups.Model}");
                                Console.WriteLine($"    Status: {ups.Variables["ups.status"]}");
                                Console.WriteLine($"    Battery: {ups.Variables["battery.charge"]}%");
                                Console.WriteLine($"    Runtime: {ups.Variables["battery.runtime"]}s");
                                Console.WriteLine();
                            }
                            break;

                        case "status" when parts.Length >= 3:
                            if (_devices.TryGetValue(parts[1], out var upsStatus))
                            {
                                upsStatus.Variables["ups.status"] = string.Join(" ", parts.Skip(2));
                                Console.WriteLine($"Set {parts[1]} status to: {upsStatus.Variables["ups.status"]}");
                            }
                            else Console.WriteLine($"UPS '{parts[1]}' not found");
                            break;

                        case "set" when parts.Length >= 4:
                            if (_devices.TryGetValue(parts[1], out var upsSet))
                            {
                                var varName = parts[2];
                                var value = string.Join(" ", parts.Skip(3));
                                upsSet.Variables[varName] = value;
                                Console.WriteLine($"Set {parts[1]}.{varName} = {value}");
                            }
                            else Console.WriteLine($"UPS '{parts[1]}' not found");
                            break;

                        case "add" when parts.Length >= 4:
                            var newName = parts[1];
                            var mfr = parts[2];
                            var model = string.Join(" ", parts.Skip(3));
                            _devices[newName] = new UpsDevice(newName, mfr, model);
                            Console.WriteLine($"Added UPS: {newName}");
                            break;

                        case "remove" when parts.Length >= 2:
                            if (_devices.Remove(parts[1]))
                                Console.WriteLine($"Removed UPS: {parts[1]}");
                            else
                                Console.WriteLine($"UPS '{parts[1]}' not found");
                            break;

                        case "discharge" when parts.Length >= 2:
                            if (_devices.TryGetValue(parts[1], out var upsDis))
                            {
                                upsDis.Variables["ups.status"] = "OB";
                                upsDis.Variables["battery.charge"] = "75";
                                upsDis.Variables["battery.runtime"] = "900";
                                Console.WriteLine($"{parts[1]}: Now on battery (75%, 900s runtime)");
                            }
                            else Console.WriteLine($"UPS '{parts[1]}' not found");
                            break;

                        case "restore" when parts.Length >= 2:
                            if (_devices.TryGetValue(parts[1], out var upsRes))
                            {
                                upsRes.Variables["ups.status"] = "OL";
                                upsRes.Variables["battery.charge"] = "100";
                                upsRes.Variables["battery.runtime"] = "1800";
                                Console.WriteLine($"{parts[1]}: Restored to online (100%, 1800s runtime)");
                            }
                            else Console.WriteLine($"UPS '{parts[1]}' not found");
                            break;

                        case "critical" when parts.Length >= 2:
                            if (_devices.TryGetValue(parts[1], out var upsCrit))
                            {
                                upsCrit.Variables["ups.status"] = "OB LB";
                                upsCrit.Variables["battery.charge"] = "10";
                                upsCrit.Variables["battery.runtime"] = "120";
                                Console.WriteLine($"{parts[1]}: CRITICAL STATE (OB LB, 10%, 120s runtime)");
                            }
                            else Console.WriteLine($"UPS '{parts[1]}' not found");
                            break;

                        case "fsd" when parts.Length >= 2:
                            if (_devices.TryGetValue(parts[1], out var upsFsd))
                            {
                                upsFsd.Variables["ups.status"] = "OB LB FSD";
                                upsFsd.Variables["battery.charge"] = "5";
                                upsFsd.Variables["battery.runtime"] = "30";
                                Console.WriteLine($"{parts[1]}: FORCED SHUTDOWN (FSD signal sent)");
                            }
                            else Console.WriteLine($"UPS '{parts[1]}' not found");
                            break;

                        case "help":
                            PrintHelp();
                            break;

                        case "quit":
                        case "exit":
                            _running = false;
                            Console.WriteLine("Shutting down...");
                            break;

                        default:
                            Console.WriteLine($"Unknown command: {cmd}. Type 'help' for commands.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }

    static async Task HandleClient(TcpClient client)
    {
        var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        Console.WriteLine($"[{endpoint}] Client connected");
        ClientServerLog.Info("[{Endpoint}] Client connected", endpoint);

        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                var response = ProcessCommand(line, endpoint);
                await writer.WriteAsync(response);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{endpoint}] Error: {ex.Message}");
            ClientServerLog.Error(ex, "[{Endpoint}] Error", endpoint);
        }
        finally
        {
            client.Close();
            Console.WriteLine($"[{endpoint}] Client disconnected");
            ClientServerLog.Info("[{Endpoint}] Client disconnected", endpoint);
        }
    }

    static string ProcessCommand(string line, string endpoint)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "ERR UNKNOWN-COMMAND\n";

        var cmd = parts[0].ToUpper();
        ClientServerLog.Debug("[{Endpoint}] << {Command}", endpoint, line);

        lock (_lock)
        {
            string response = cmd switch
            {
                "LIST" when parts.Length >= 2 => HandleList(parts),
                "GET" when parts.Length >= 3 => HandleGet(parts),
                "USERNAME" => "OK\n",
                "PASSWORD" => "OK\n",
                "LOGIN" => "OK\n",
                "LOGOUT" => "OK Goodbye\n",
                "VER" => "Mock NUT Server 1.0\n",
                "NETVER" => "1.2\n",
                _ => "ERR UNKNOWN-COMMAND\n"
            };

            ClientServerLog.Debug("[{Endpoint}] >> {Response}", endpoint, response.TrimEnd());
            return response;
        }
    }

    static string HandleList(string[] parts)
    {
        var subCmd = parts[1].ToUpper();

        switch (subCmd)
        {
            case "UPS":
                var sb = new StringBuilder();
                sb.AppendLine("BEGIN LIST UPS");
                foreach (var (name, ups) in _devices)
                {
                    sb.AppendLine($"UPS {name} \"{ups.Description}\"");
                }
                sb.AppendLine("END LIST UPS");
                return sb.ToString();

            case "VAR" when parts.Length >= 3:
                var upsName = parts[2];
                if (!_devices.TryGetValue(upsName, out var device))
                    return $"ERR UNKNOWN-UPS {upsName}\n";

                var varsb = new StringBuilder();
                varsb.AppendLine($"BEGIN LIST VAR {upsName}");
                foreach (var (varName, value) in device.Variables)
                {
                    varsb.AppendLine($"VAR {upsName} {varName} \"{value}\"");
                }
                varsb.AppendLine($"END LIST VAR {upsName}");
                return varsb.ToString();

            default:
                return "ERR INVALID-ARGUMENT\n";
        }
    }

    static string HandleGet(string[] parts)
    {
        var subCmd = parts[1].ToUpper();

        if (subCmd == "VAR" && parts.Length >= 4)
        {
            var upsName = parts[2];
            var varName = parts[3];

            if (!_devices.TryGetValue(upsName, out var device))
                return $"ERR UNKNOWN-UPS {upsName}\n";

            if (!device.Variables.TryGetValue(varName, out var value))
                return $"ERR VAR-NOT-SUPPORTED {varName}\n";

            return $"VAR {upsName} {varName} \"{value}\"\n";
        }

        if (subCmd == "UPSDESC" && parts.Length >= 3)
        {
            var upsName = parts[2];
            if (!_devices.TryGetValue(upsName, out var device))
                return $"ERR UNKNOWN-UPS {upsName}\n";

            return $"UPSDESC {upsName} \"{device.Description}\"\n";
        }

        return "ERR INVALID-ARGUMENT\n";
    }
}

class UpsDevice
{
    public string Name { get; }
    public string Manufacturer { get; }
    public string Model { get; }
    public string Description => $"{Manufacturer} {Model}";
    public Dictionary<string, string> Variables { get; } = new();

    public UpsDevice(string name, string manufacturer, string model)
    {
        Name = name;
        Manufacturer = manufacturer;
        Model = model;

        // Initialize default variables
        Variables["device.mfr"] = manufacturer;
        Variables["device.model"] = model;
        Variables["device.type"] = "ups";
        Variables["ups.mfr"] = manufacturer;
        Variables["ups.model"] = model;
        Variables["ups.status"] = "OL";
        Variables["ups.load"] = "25";
        Variables["ups.temperature"] = "35";
        Variables["battery.charge"] = "100";
        Variables["battery.charge.low"] = "20";
        Variables["battery.charge.warning"] = "50";
        Variables["battery.runtime"] = "1800";
        Variables["battery.runtime.low"] = "120";
        Variables["battery.voltage"] = "27.0";
        Variables["battery.voltage.nominal"] = "24.0";
        Variables["battery.type"] = "PbAc";
        Variables["input.voltage"] = "120.0";
        Variables["input.voltage.nominal"] = "120";
        Variables["input.frequency"] = "60.0";
        Variables["input.transfer.high"] = "130";
        Variables["input.transfer.low"] = "100";
        Variables["output.voltage"] = "120.0";
        Variables["output.frequency"] = "60.0";
        Variables["output.current"] = "2.5";
        Variables["ups.power"] = "300";
        Variables["ups.power.nominal"] = "1500";
        Variables["ups.realpower"] = "285";
        Variables["ups.realpower.nominal"] = "900";
        Variables["ups.beeper.status"] = "enabled";
        Variables["ups.delay.shutdown"] = "20";
        Variables["ups.delay.start"] = "30";
        Variables["ups.timer.shutdown"] = "-1";
        Variables["ups.timer.start"] = "-1";
    }
}
