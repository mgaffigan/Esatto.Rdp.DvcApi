using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Xml;

namespace Esatto.Win32.Rdp.DvcApi.WcfDvc
{
    class DvcBindingElement
        : TransportBindingElement // to signal that we're a transport
        , IPolicyExportExtension // for policy export
    {
        public DvcBindingElement()
            : base()
        {
        }

        protected DvcBindingElement(DvcBindingElement other)
            : base(other)
        {
        }

        public override string Scheme
        {
            get { return "esbkdvc"; }
        }

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            return (IChannelFactory<TChannel>)(object)new DvcChannelFactory(this, context);
        }

        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            return (IChannelListener<TChannel>)(object)new DvcChannelListener(this, context);
        }

        // We only support IDuplexSession for our client ChannelFactories
        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        {
            if (typeof(TChannel) == typeof(IDuplexSessionChannel))
            {
                return true;
            }

            return false;
        }

        // We only support IDuplexSession for our Listeners
        public override bool CanBuildChannelListener<TChannel>(BindingContext context)
        {
            if (typeof(TChannel) == typeof(IDuplexSessionChannel))
            {
                return true;
            }

            return false;
        }

        public override BindingElement Clone()
        {
            return new DvcBindingElement(this);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // default to MTOM if no encoding is specified
            if (context.BindingParameters.Find<MessageEncodingBindingElement>() == null)
            {
                context.BindingParameters.Add(new MtomMessageEncodingBindingElement());
            }

            return base.GetProperty<T>(context);
        }

        // We expose in policy The fact that we're TCP.
        // Import is done through TcpBindingElementImporter.
        void IPolicyExportExtension.ExportPolicy(MetadataExporter exporter, PolicyConversionContext context)
        {
            if (exporter == null)
            {
                throw new ArgumentNullException("exporter");
            }

            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ICollection<XmlElement> bindingAssertions = context.GetBindingAssertions();
            XmlDocument xmlDocument = new XmlDocument();
            const string prefix = "bk";
            const string transportAssertion = "esbkdvc";
            const string tcpPolicyNamespace = "urn:rmg:esbkdvc:nb";
            bindingAssertions.Add(xmlDocument.CreateElement(prefix, transportAssertion, tcpPolicyNamespace));
        }
    }
}
