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
        private IRawDvcChannel target;

        public MainWindow()
        {
            InitializeComponent();
            this.Title = Guid.NewGuid().ToString().Substring(0, 6);
            this.ServiceRegistration = new BrokeredServiceRegistration(Title, AcceptConnection);
        }

        private void AcceptConnection(IRawDvcChannel obj)
        {
            this.target = obj;
            obj.MessageReceived += (_1, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    listBox.Items.Add(Encoding.UTF8.GetString(e.Message));
                });
            };
            obj.SenderDisconnected += (_1, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    listBox.Items.Add("Disconnected");
                });
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            ServiceRegistration.Dispose();
        }

        private void _bt_Click(object sender, RoutedEventArgs e)
        {
            target?.SendMessage(Encoding.UTF8.GetBytes(textBox.Text));
        }
    }
}
