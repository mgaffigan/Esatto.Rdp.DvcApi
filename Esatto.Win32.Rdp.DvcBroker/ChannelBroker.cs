using Esatto.Win32.Rdp.DvcApi.Broker;
using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.Broker
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId(DvcBrokerConstants.BrokerProgId)]
    // Must be public for regasm
    public class ChannelBroker : IDvcChannelBroker
    {
        private readonly object syncRegisteredServices = new object();
        private readonly Dictionary<string, ServiceRegistration> RegisteredServices
            = new Dictionary<string, ServiceRegistration>(StringComparer.OrdinalIgnoreCase);

        // COM Endpoint
        public IBrokeredDvcServiceRegistration RegisterService(string name, IBrokeredDvcService service)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            ServiceRegistration existingRegistration;
            lock (syncRegisteredServices)
            {
                RegisteredServices.TryGetValue(name, out existingRegistration);
            }

            if (existingRegistration != null)
            {
                if (existingRegistration.CheckIsAlive())
                {
                    throw new InvalidOperationException($"Service '{name}' is already registered");
                }
                else
                {
                    try
                    {
                        existingRegistration.Dispose();
                        lock (syncRegisteredServices)
                        {
                            if (RegisteredServices[name] != existingRegistration)
                            {
                                throw new InvalidOperationException("TOCTOU while cleaning up disconnected service");
                            }
                            RegisteredServices.Remove(name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Could not clean up disconnected registration {existingRegistration}");
                    }
                }
            }

            // May leak if TOCTOU
            existingRegistration = new ServiceRegistration(name, service);
            lock (syncRegisteredServices)
            {
                // May throw if TOCTOU... I'm fine with that.
                RegisteredServices.Add(name, existingRegistration);
            }
            return new UnregisterProxy(this, existingRegistration);
        }

        public void AcceptConnection(RawDynamicVirtualClientChannel channel)
        {
            channel.MessageReceived += this.unk_MessageReceived;
            channel.SenderDisconnected += this.unk_SenderDisconnected;
            channel.Exception += this.unk_Exception;
        }

        private void unk_Exception(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error((Exception)e.ExceptionObject, "Exception on read thread");
        }

        private void unk_SenderDisconnected(object sender, CloseReceivedEventArgs e)
        {
            Logger.Error(new InvalidOperationException(), "Connection ended without specifying service name");
        }

        private void unk_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            var channel = (RawDynamicVirtualClientChannel)sender;
            channel.MessageReceived -= unk_MessageReceived;
            channel.SenderDisconnected -= this.unk_SenderDisconnected;

            try
            {
                var serviceName = Encoding.UTF8.GetString(e.Message);
                ServiceRegistration existingRegistration;
                lock (syncRegisteredServices)
                {
                    if (!RegisteredServices.TryGetValue(serviceName, out existingRegistration))
                    {
                        channel.SendMessage(Encoding.UTF8.GetBytes("ERROR Service not registered"));
                        throw new ArgumentOutOfRangeException($"Service '{serviceName}' not registered");
                    }
                }
                channel.SendMessage(Encoding.UTF8.GetBytes("ACCEPTED"));
                existingRegistration.AcceptConnection(channel);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not establish connection");
            }
        }

        private class UnregisterProxy : IBrokeredDvcServiceRegistration
        {
            private readonly ChannelBroker Broker;
            private readonly WeakReference<ServiceRegistration> Registration;

            public UnregisterProxy(ChannelBroker channelBroker, ServiceRegistration existingRegistration)
            {
                this.Broker = channelBroker;
                this.Registration = new WeakReference<ServiceRegistration>(existingRegistration);
            }

            public void Unregister()
            {
                lock (Broker.syncRegisteredServices)
                {
                    if (Registration.TryGetTarget(out var target)
                        && Broker.RegisteredServices.TryGetValue(target.ServiceName, out var currentRegistration)
                        && ReferenceEquals(currentRegistration, target))
                    {
                        Broker.RegisteredServices.Remove(target.ServiceName);
                    }
                }
            }
        }

        #region Registration

        [ComRegisterFunction]
        internal static void RegasmRegisterLocalServer(string path)
        {
            // path is HKEY_CLASSES_ROOT\\CLSID\\{clsid}", we only want CLSID...
            path = path.Substring("HKEY_CLASSES_ROOT\\".Length);
            using (RegistryKey keyCLSID = Registry.ClassesRoot.OpenSubKey(path, writable: true))
            {
                // Remove the auto-generated InprocServer32 key after registration
                // (REGASM puts it there but we are going out-of-proc).
                keyCLSID.DeleteSubKeyTree("InprocServer32");

                // Create "LocalServer32" under the CLSID key
                using (RegistryKey subkey = keyCLSID.CreateSubKey("LocalServer32"))
                {
                    subkey.SetValue("", Assembly.GetExecutingAssembly().Location, RegistryValueKind.String);
                }
            }
        }

        [ComUnregisterFunction]
        internal static void RegasmUnregisterLocalServer(string path)
        {
            // path is HKEY_CLASSES_ROOT\\CLSID\\{clsid}", we only want CLSID...
            path = path.Substring("HKEY_CLASSES_ROOT\\".Length);
            Registry.ClassesRoot.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }

        #endregion
    }
}
