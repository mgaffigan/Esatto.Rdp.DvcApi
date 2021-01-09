using Esatto.Win32.Rdp.DvcApi;
using Esatto.Win32.Rdp.DvcApi.Broker;
using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestApp.Brokered.OnRdpClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private BrokeredServiceRegistration ServiceRegistration;
        private IAsyncDvcChannel target;

        public MainWindow()
        {
            InitializeComponent();
            this.Title = Guid.NewGuid().ToString().Substring(0, 6);
            this.ServiceRegistration = new BrokeredServiceRegistration(Title, c => AcceptConnection(c));
        }

        private void AcceptConnection(IAsyncDvcChannel obj)
        {
            this.target = obj;
            Dispatcher.Invoke(() =>
            {
                ReadAsync(obj);
            });
            obj.Disconnected += (_1, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    listBox.Items.Add("Disconnected");
                });
            };
        }

        private async void ReadAsync(IAsyncDvcChannel obj)
        {
            try
            {
                while (true)
                {
                    listBox.Items.Add(Encoding.UTF8.GetString(await obj.ReadMessageAsync()));
                }
            }
            catch (TaskCanceledException)
            {
                listBox.Items.Add("Ending read due to cancelled");
            }
            catch (ObjectDisposedException)
            {
                listBox.Items.Add("Ending read due to ODE");
            }
            catch (DvcChannelDisconnectedException)
            {
                listBox.Items.Add("Ending read due to disconnected");
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            ServiceRegistration.Dispose();
        }

        private void _bt_Click(object sender, RoutedEventArgs e)
        {
            var rs = Encoding.UTF8.GetBytes(textBox.Text);
            target?.SendMessage(rs, 0, rs.Length);
        }
    }
}
