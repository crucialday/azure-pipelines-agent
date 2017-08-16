using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Capabilities
{
    public sealed class WindowsCapabilitiesProvider : AgentService, ICapabilitiesProvider
    {
        public Type ExtensionType => typeof(ICapabilitiesProvider);

        // Only runs on Windows.
        public int Order => 2;

        public async Task<List<Capability>> GetCapabilitiesAsync(AgentSettings settings, CancellationToken cancellationToken)
        {
            Trace.Entering();
            var capabilities = new List<Capability>();

            var capabilityProviders = new List<IPrivateWindowsCapabilityProvider>
            {
                // new AndroidSdkCapabilities(), 
                new AntCapabilities(), 
                // new AzureGuestAgentCapabilities(), 
                // new AzurePowerShellCapabilities(), 
                // new ChefCapabilities(), 
                // new DotNetFrameworkCapabilities(), 
                // new JavaCapabilities(), 
                // new MavenCapabilities(), 
                // new MSBuildCapabilities(), 
                // new NodeToolCapabilities(), 
                // new PowerShellCapabilities(), 
                // new ScvmmAdminConsoleCapabilities(), 
                // new SqlPackageCapabilities(), 
                // new VisualStudioCapabilities(), 
                // new WindowsKitCapabilities(), 
                // new WindowsSdkCapabilities(), 
                // new XamarinAndroidCapabilities()
            };

            foreach (IPrivateWindowsCapabilityProvider provider in capabilityProviders)
            {
                capabilities.AddRange(provider.GetCapabilities());
            }

            //string powerShellExe = HostContext.GetService<IPowerShellExeUtil>().GetPath();
            //string scriptFile = Path.Combine(HostContext.GetDirectory(WellKnownDirectory.Bin), "powershell", "Add-Capabilities.ps1").Replace("'", "''");
            //ArgUtil.File(scriptFile, nameof(scriptFile));
            //string arguments = $@"-NoLogo -Sta -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -Command "". '{scriptFile}'""";
            // using (var processInvoker = HostContext.CreateService<IProcessInvoker>())
            // {
            //     processInvoker.OutputDataReceived +=
            //         (object sender, ProcessDataReceivedEventArgs args) =>
            //         {
            //             Trace.Info($"STDOUT: {args.Data}");
            //             Capability capability;
            //             if (TryParseCapability(args.Data, out capability))
            //             {
            //                 Trace.Info($"Adding '{capability.Name}': '{capability.Value}'");
            //                 capabilities.Add(capability);
            //             }
            //         };
            //     processInvoker.ErrorDataReceived +=
            //         (object sender, ProcessDataReceivedEventArgs args) =>
            //         {
            //             Trace.Info($"STDERR: {args.Data}");
            //         };
            //     await processInvoker.ExecuteAsync(
            //         workingDirectory: Path.GetDirectoryName(scriptFile),
            //         fileName: powerShellExe,
            //         arguments: arguments,
            //         environment: null,
            //         requireExitCodeZero: false,
            //         outputEncoding: null,
            //         killProcessOnCancel: true,
            //         cancellationToken: cancellationToken);
            // }

            return capabilities;
        }

        // public bool TryParseCapability(string input, out Capability capability)
        // {
        //     Command command;
        //     string name;
        //     if (Command.TryParse(input, out command) &&
        //         string.Equals(command.Area, "agent", StringComparison.OrdinalIgnoreCase) &&
        //         string.Equals(command.Event, "capability", StringComparison.OrdinalIgnoreCase) &&
        //         command.Properties.TryGetValue("name", out name) &&
        //         !string.IsNullOrEmpty(name))
        //     {
        //         capability = new Capability(name, command.Data);
        //         return true;
        //     }

        //     capability = null;
        //     return false;
        // }

        private interface IPrivateWindowsCapabilityProvider
        {
            List<Capability> GetCapabilities();
        }

        private interface IRegistryService
        {
            // TODO: Write methods and implement. Inject in Capabilities classes.
        }

        private interface IEnvironmentService
        {
            // TODO: Write methods and implement. Inject in Capabilities classes.
        }

        public static class CapabilityNames
        {
            public static string AndroidSdk = "AndroidSDK";
            public static string AzureGuestAgent = "AzureGuestAgent";
            public static string Chef = "Chef";
        }

        private class AndroidSdkCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();
                // Do this when we add any capability
                //Trace.Info($"Adding '{capability.Name}': '{capability.Value}'");

                string androidSdkPath = GetAndroidSdkPath();

                if (!string.IsNullOrEmpty(androidSdkPath))
                {
                    // Add the capability
                    // TODO: Write to host. We can probably put this in a special collection that writes to host when items are added? Something to reuse code.
                    capabilities.Add(new Capability(CapabilityNames.AndroidSdk, androidSdkPath));

                    // Check if the platforms directory exists
                    string platformsDirectory = Path.Combine(androidSdkPath, "platforms");

                    if (Directory.Exists(platformsDirectory))
                    {
                        foreach (string platformDir in Directory.GetDirectories(platformsDirectory))
                        {
                            string capabilityName = new DirectoryInfo(platformDir).Name.Replace("android-", CapabilityNames.AndroidSdk + "_");
                            capabilities.Add(new Capability(capabilityName, platformDir));
                        }
                    }
                }

                return capabilities;
            }

            private string GetAndroidSdkPath()
            {
                // Attempt to get it from ANDROID_HOME environment variable
                string envVar = Environment.GetEnvironmentVariable("ANDROID_HOME");
                if (!string.IsNullOrEmpty(envVar))
                {
                    // Write-Host "Found ANDROID_HOME from machine environment."
                    return envVar;
                }

                // Attempt to get from registry info
                var hiveViewPairs = new List<HiveViewPair>
                {
                    new HiveViewPair("CurrentUser", "Default"), 
                    new HiveViewPair("LocalMachine", "Registry64"), 
                    new HiveViewPair("LocalMachine", "Registry32")
                };

                foreach (HiveViewPair pair in hiveViewPairs)
                {
                    string registryValue = GetRegistryValue(pair.Hive, pair.View, "SOFTWARE\\Android SDK Tools", "Path");

                    if (!string.IsNullOrEmpty(registryValue))
                    {
                        return registryValue.Trim();
                    }
                }

                return null;
            }

            private string GetRegistryValue(string hive, string view, string keyName, string valueName) // TODO: In some format, this should go in an IRegistryService I think, for testing.
            {
                if (view == "Registry64" && 
                    !System.Environment.Is64BitOperatingSystem)
                {
                    // TODO: Log... "Skipping."
                    return null;
                }

                Win32.RegistryKey baseKey = null;
                Win32.RegistryKey subKey = null;

                try
                {
                    baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
                    subKey = baseKey.OpenSubKey(keyName);

                    var value = subKey.GetValue(valueName);

                    if (value != null)
                    {
                        string sValue = value as string;

                        if (!string.IsNullOrEmpty(sValue))
                        {
                            // TODO: Write that we found it
                            return sValue;
                        }
                    }
                    else
                    {
                        // TODO: Write that we didn't find it
                        return null;
                    }
                }
                finally
                {
                    if (baseKey != null) { baseKey.Dispose(); }
                    if (subKey != null) { subKey.Dispose(); }
                }

                return null;
            }
        }

        private class HiveViewPair
        {
            public HiveViewPair(string hive, string view)
            {
                Hive = hive;
                View = view;
            }

            public string Hive { get; }
            public string View { get; }
        }

        private class AntCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();

                var environmentCapability = new EnvironmentVariableCapability(name: "ant", variableName: "ANT_HOME");
                // TODO: 
                // Add-CapabilityFromEnvironment -Name 'ant' -VariableName 'ANT_HOME'
                // TODO: Can we put all the capabilities from environment into one EnvironmentCapabilities: IPrivateWindowsCapabilityProvider class?
                // if (AttemptAddEnvironmentCapability(environmentCapability))
                // {
                //     capabilities.Add()
                // }

                // TODO: Trace... checking for value ant and variable name ANT_HOME
                string value = Environment.GetEnvironmentVariable(environmentCapability.VariableName);
                if (!string.IsNullOrEmpty(value))
                {
                    // The environment variable exists
                    var capability = new Capability(environmentCapability.Name, value);
                    //Trace.Info($"Adding '{capability.Name}': '{capability.Value}'");
                    capabilities.Add(capability);
                }

                return capabilities;
            }

            // private bool AttemptAddEnvironmentCapability(EnvironmentVariableCapability environmentCapability)
            // {
            //     Environment.GetEnvironmentVariable("ANT_HOME");


            //     return false;
            // }
        }

        private class EnvironmentVariableCapability
        {
            public EnvironmentVariableCapability(string name, string variableName)
            {
                Name = name;
                VariableName = variableName;
            }

            public string Name {get;}
            public string VariableName {get;}
        }

        private class AzureGuestAgentCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();

                Process runningProcess = Process.GetProcessesByName("WindowsAzureGuestAgent").FirstOrDefault();

                if (runningProcess == null)
                {
                    // TODO: Log that we couldnt find WindowsAzureGuestAgent
                }
                else
                {
                    // TODO: Log that we found WindowsAzureGuestAgent
                    // TODO: Make sure runningProcess.MainModule.FileName is right
                    // TODO: Abstract getting the name and file of a running process?
                    capabilities.Add(new Capability(CapabilityNames.AzureGuestAgent, runningProcess.MainModule.FileName)); 
                }

                // TODO: Is the best way to get this to look at the list of running processes? Is there something static we can check or does it have to be running?

                return capabilities;
            }
        }

        private class AzurePowerShellCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();


                return capabilities;
            }
        }

        private class ChefCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();

                // Attempt to get location from Registry
                string version = GetVersionFromRegistry();

                // Get the chef directory from PATH
                string chefDirectory = GetChefDirectoryFromPath();

                // Add capabilities
                if (!string.IsNullOrEmpty(version) && 
                    !string.IsNullOrEmpty(chefDirectory))
                {
                    // chef
                    // Write-Capability -Name 'Chef' -Value $version // TODO: Would this even work correctly? It's adding the version but not the path
                    capabilities.Add(new Capability(CapabilityNames.Chef, version));

                    // Add-KnifeCapabilities -ChefDirectory $chefDirectory


                }

                return capabilities;
            }

            private string GetVersionFromRegistry()
            {


                return null;
            }

            private string GetChefDirectoryFromPath()
            {


                return null;
            }
        }

        private class DotNetFrameworkCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();


                return capabilities;
            }
        }

        private class JavaCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();


                return capabilities;
            }
        }

        //

        private class MavenCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();

                // TODO: This is an example of something we could add to a general EnvironmentCapabilities : IPrivateWindowsCapabilityProvider
                // Write-Host "Checking: env:JAVA_HOME"
                // if (!$env:JAVA_HOME) {
                //     Write-Host "Value not found or empty."
                //     return
                // }

                // Add-CapabilityFromEnvironment -Name 'maven' -VariableName 'M2_HOME'


                return capabilities;
            }
        }

        private class MSBuildCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();


                return capabilities;
            }
        }

        private class NodeToolCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();

                // Add-CapabilityFromApplication -Name 'npm' -ApplicationName 'npm'
                // Add-CapabilityFromApplication -Name 'gulp' -ApplicationName 'gulp'
                // Add-CapabilityFromApplication -Name 'node.js' -ApplicationName 'node'
                // Add-CapabilityFromApplication -Name 'bower' -ApplicationName 'bower'
                // Add-CapabilityFromApplication -Name 'grunt' -ApplicationName 'grunt'
                // Add-CapabilityFromApplication -Name 'svn' -ApplicationName 'svn'

                return capabilities;
            }
        }

        private class PowerShellCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();

                //Write-Capability -Name 'PowerShell' -Value $PSVersionTable.PSVersion


                return capabilities;
            }
        }

        private class ScvmmAdminConsoleCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();
                
                // TODO: Can we combine into Capabilities that come from the registry?
                // foreach ($view in @('Registry64', 'Registry32')) {
                //     if ((Add-CapabilityFromRegistry -Name 'SCVMMAdminConsole' -Hive 'LocalMachine' -View $view -KeyName 'Software\Microsoft\Microsoft System Center Virtual Machine Manager Administrator Console\Setup' -ValueName 'InstallPath')) {
                //         break
                //     }
                // }

                return capabilities;
            }
        }

        private class SqlPackageCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();


                return capabilities;
            }
        }

        private class VisualStudioCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();


                return capabilities;
            }
        }

        private class WindowsKitCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();


                return capabilities;
            }
        }

        private class WindowsSdkCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();


                return capabilities;
            }
        }

        private class XamarinAndroidCapabilities : IPrivateWindowsCapabilityProvider
        {
            public List<Capability> GetCapabilities()
            {
                var capabilities = new List<Capability>();

                // TODO: This can at least be combined with ScvmmAdminConsoleCapabilities since they both use the registry
                // $null = Add-CapabilityFromRegistry -Name 'Xamarin.Android' -Hive 'LocalMachine' -View 'Registry32' -KeyName 'Software\Novell\Mono for Android' -ValueName 'InstalledVersion'


                return capabilities;
            }
        }
    }
}