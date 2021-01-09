using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Esatto.Rdp.DvcApi.Broker;
using static Esatto.Rdp.DvcApi.Broker.BrokerConstants;
using Esatto.Rdp.DvcApi;

namespace Esatto.Rdp.DvcBroker
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [Guid(BrokerClsid)]
    [ProgId(BrokerProgId)]
    // Must be public for regasm
    public class ChannelBroker : IDvcChannelBroker
    {
        private readonly object syncRegisteredServices = new object();
        private readonly Dictionary<string, ServiceRegistration> RegisteredServices
            = new Dictionary<string, ServiceRegistration>(StringComparer.OrdinalIgnoreCase);

        // COM Endpoint from IDvcChannelBroker
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
                    KillRegistration(existingRegistration);
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

        private void KillRegistration(ServiceRegistration existingRegistration)
        {
            try
            {
                existingRegistration.Dispose();
                lock (syncRegisteredServices)
                {
                    if (RegisteredServices[existingRegistration.ServiceName] != existingRegistration)
                    {
                        throw new InvalidOperationException("TOCTOU while cleaning up disconnected service");
                    }
                    RegisteredServices.Remove(existingRegistration.ServiceName);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Could not clean up disconnected registration {existingRegistration}");
            }
        }

        // Called from DVC API
        public async void AcceptConnection(IAsyncDvcChannel channel)
        {
            ServiceRegistration existingRegistration;
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                // Read service name
                var serviceNameUtf8 = await channel.ReadMessageAsync(cts.Token).ConfigureAwait(false);
                var serviceName = Encoding.UTF8.GetString(serviceNameUtf8);

                // Lookup service
                lock (syncRegisteredServices)
                {
                    if (!RegisteredServices.TryGetValue(serviceName, out existingRegistration))
                    {
                        throw new ChannelNotAvailableException($"Service '{serviceName}' not registered");
                    }
                }

                // Accept
                await channel.SendMessageAsync(Encoding.UTF8.GetBytes("ACCEPTED")).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception trying to accept a new brokered connection");

                try
                {
                    await channel.SendMessageAsync(Encoding.UTF8.GetBytes("ERROR Service not registered")).ConfigureAwait(false);
                    channel.Dispose();
                }
                catch (OperationCanceledException) { /* no-op */ }
                catch (ObjectDisposedException) { /* no-op */ }
                catch (DvcChannelDisconnectedException) { /* no-op */ }
                catch (Exception ex2)
                {
                    Logger.Error(ex2, "Could not notify RDS endpoint of failed conncetion attempt");
                }
                return;
            }

            if (!existingRegistration.TryAcceptConnection(channel))
            {
                KillRegistration(existingRegistration);
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
