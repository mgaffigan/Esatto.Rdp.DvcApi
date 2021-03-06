﻿using Esatto.Rdp.DvcApi.WcfDvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace TestApp.WcfBrokered.OnRdpClient
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false)]
    class SampleServer : IServer
    {
        public byte[] DoThing(string foo)
        {
            Console.WriteLine($"Received {foo}");
            var d = new byte[64];
            var rand = new System.Security.Cryptography.RNGCryptoServiceProvider();
            rand.GetBytes(d);
            return d;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var sh = new ServiceHost(new SampleServer());

            ServiceThrottlingBehavior throttle;
            throttle = sh.Description.Behaviors.Find<ServiceThrottlingBehavior>();
            if (throttle == null)
            {
                throttle = new ServiceThrottlingBehavior();
                throttle.MaxConcurrentCalls = 500;
                throttle.MaxConcurrentSessions = 250;
                throttle.MaxConcurrentInstances = 750;
                sh.Description.Behaviors.Add(throttle);
            }

            var binding = new DvcBinding();
            sh.AddServiceEndpoint(typeof(IServer), binding, "esbkdvc:///demoService");
            sh.Open();
            Console.ReadLine();
            sh.Close();
        }
    }

    [ServiceContract(Namespace = "urn:example")]
    public interface IServer
    {
        [OperationContract]
        byte[] DoThing(string foo);
    }
}
