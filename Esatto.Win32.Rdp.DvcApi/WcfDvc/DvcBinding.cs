using System.ServiceModel.Channels;

namespace Esatto.Rdp.DvcApi.WcfDvc
{
    public class DvcBinding : Binding, IBindingRuntimePreferences
    {
        DvcBindingElement transport;
        BinaryMessageEncodingBindingElement encoding;

        public DvcBinding()
            : this(256)
        {
        }

        public DvcBinding(int mbMaxRead)
        {
            transport = new DvcBindingElement();
            encoding = new BinaryMessageEncodingBindingElement();

            const int mb = 1024 /* kb */ * 1024;
            var limit = mbMaxRead * mb;
            transport.MaxBufferPoolSize = limit;
            transport.MaxReceivedMessageSize = limit;
            encoding.MaxWritePoolSize = limit;
            encoding.ReaderQuotas.MaxBytesPerRead = limit;
            encoding.ReaderQuotas.MaxStringContentLength = limit;
            encoding.ReaderQuotas.MaxArrayLength = limit;
        }

        bool IBindingRuntimePreferences.ReceiveSynchronously
        {
            get { return false; }
        }

        public override string Scheme { get { return transport.Scheme; } }

        public override BindingElementCollection CreateBindingElements()
        {
            var bindingElements = new BindingElementCollection();
            bindingElements.Add(encoding);
            bindingElements.Add(transport);
            return bindingElements.Clone();
        }
    }
}
