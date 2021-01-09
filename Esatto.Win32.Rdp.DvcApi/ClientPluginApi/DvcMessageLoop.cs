#define TRACE

using Esatto.Win32.Com;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using static Esatto.Win32.Com.ComInterop;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    // Runs on RDS Client, message pump for out-of-process plugin
    public class DvcMessageLoop : ApplicationContext
    {
        private readonly ClassObjectRegistration PluginRegistration;

        public DvcMessageLoop(Dictionary<string, Action<IAsyncDvcChannel>> registeredChannels, Guid clsid, bool resumeClassObjects = true)
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            // register COM objects
            this.PluginRegistration = new ClassObjectRegistration(clsid,
                CreateClassFactoryFor(() => new DelegateWtsClientPlugin(registeredChannels)), CLSCTX.LOCAL_SERVER, REGCLS.MULTIPLEUSE | REGCLS.SUSPENDED);
            if (resumeClassObjects)
            {
                CoResumeClassObjects();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                this.PluginRegistration.Dispose();
            }
        }

        public static void Register(Guid wtsPluginClsid, string progid, string startCommand)
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

        public static void Unregister(Guid wtsPluginClsid)
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
