#define TRACE

using Esatto.Win32.Com;
using Microsoft.Win32;
using System;
using System.Threading;
using System.Windows.Forms;
using static Esatto.Win32.Com.ComInterop;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    sealed class DvcMessageLoop : ApplicationContext
    {
        private readonly ClassObjectRegistration CoordinatorRegistration;
        private const int EventIdStart = 20;

        public DvcMessageLoop(IDynamicVirtualClientChannelFactory[] channelHosts, Guid clsid)
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            // register COM objects
            this.CoordinatorRegistration = new ClassObjectRegistration(clsid,
                CreateClassFactoryFor(() => new DelegateWtsClientPlugin(channelHosts)), CLSCTX.LOCAL_SERVER, REGCLS.MULTIPLEUSE | REGCLS.SUSPENDED);
            CoResumeClassObjects();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                this.CoordinatorRegistration.Dispose();
            }
        }

        internal static void Register(Guid wtsPluginClsid, string progid, string startCommand)
        {
            var clsid = wtsPluginClsid.ToString("b").ToUpperInvariant();
            using (var hkcr64 = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64))
            using (var clreg = hkcr64.CreateSubKey($@"CLSID\{clsid}", writable: true))
            {
                clreg.SetValue(null, progid);

                using (var ls32 = clreg.CreateSubKey("LocalServer32", writable: true))
                {
                    ls32.SetValue(null, $"\"{startCommand}\"");
                }
            }

            using (var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var tscaddin = hklm64.CreateSubKey($@"SOFTWARE\Microsoft\Terminal Server Client\Default\AddIns\{clsid}", writable: true))
            {
                tscaddin.SetValue("Name", clsid);
            }
        }

        internal static void Unregister(Guid wtsPluginClsid)
        {
            var clsid = wtsPluginClsid.ToString("b").ToUpperInvariant();

            using (var hkcr64 = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64))
            {
                hkcr64.DeleteSubKeyTree($@"CLSID\{clsid}");
            }
            using (var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                hklm64.DeleteSubKeyTree($@"SOFTWARE\Microsoft\Terminal Server Client\Default\AddIns\{clsid}");
            }
        }
    }
}
