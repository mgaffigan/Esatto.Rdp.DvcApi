# RDP Virtual Channel API

Multiple models for communicating across RDP using the mstsc plugin API.  Client refers to the 
RDP Client (though the client hosts DVC Services).  Server refers to the session host (remote 
RDP Server - which typically acts as the client to DVC Services).

1. Raw (`PluginApplication` on client / `DvcServerChannel` on session host)<br/>Implement a completely custom DVC.  Client side runs in the mstsc Plugin process.
2. Brokered channel (`BrokeredServiceRegistration` on client / `BrokeredServiceClient` on session host)<br/>Implement a custom DVC where the mstsc plugin passes data to other processes which can dynamically register services available to the remote applications running on the session host server.
3. WCF Brokered Servcice (`DvcBinding` on both ends)<br/>A WCF Binding which allows for services to be registered with broker on the client.  The same binding allows applications on the session host server to access the client services.
