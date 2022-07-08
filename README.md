![big image](/MUSE-RPLogo.png)
# MUSE-RP-RUDP-protocol
MUSE-RP(Multiplayer UDP Service Extension-Reliable Protocol) is a videogame RUDP protocol, written in C#. Inspired by TCP mechanisms, MUSE-RP attempts to incorporate the best of both worlds: TCP (reliability) and UDP (speed). It arose from the need for a protocol that can be adapted to the specific characteristics of video games. MUSE-RP is a fully configurable and open source protocol. 
## Features
### Client-Server architecture
Use of specific classes for the execution of a client or a server. Both inherit from the Host class, which can be modified and configured to create other types of architectures.
### Two communication channels
Two delivery modes through two different channels and ports:
- Fully reliable, inspired by TCP.
- Partially reliable, whose tolerable loss percentage is customizable.


Both of them guarantee an orderly delivery
### Connection-oriented mechanisms
Implementation of INIT messages to initiate communication, and END messages to terminate the connection.
### Ping messages
Use of a proprietary ping mechanism, whose RTT calculation is used to adapt the timers that trigger message retransmissions.
They also close connections that have not answered within a certain (and configurable) time.
The ping interval is also indicated by the programmer.
### Fast retrasmission
Like TCP, MUSE-RP will retransmit the oldest unacknowledged message if it receives three duplicate ACKs.
### Message Handlers
Use of message type handlers that simplify the development of the application.
### Lightweight header
MUSE-RP uses a 11 byte header for each package.
## Usage
A sample implementation of MUSE-RP in a multiplayer video game developed with Unity:


https://github.com/Celtia-Martin/MUSERP-Test
### Client example configuration
```csharp
	//Initialization
	ConnectionInfo serverInfo = new ConnectionInfo("127.0.0.1", 7777, 0);
	HostOptions options = new HostOptions()
        {
            maxConnections = 1,
            timeOut = 30000,
            timePing = 1000,
            windowSize = 1000,
            timerTime = 200,
	    messageHandler =  new MessageHandler(),
            waitTime = 1

        };
	options.messageHandler.AddHandler(1, (message,source)=>Console.WriteLine("New friend just connected"));
	options.messageHandler.AddHandler(2, (message,source)=>Console.WriteLine("Server received my message"));
	int timeOutConnection = 30000;
	int connectionTries = 3;
	bool useProcessingThread = true;
        Client client = new Client(options, serverInfo, timeOutConnection, connectionTries, useProcessingThread);
	//Start
	client.AddOnConnectedHandler(()=>client.SendToServer(1,reliable,ASCIIEncoding.GetBytes("Hello there!")));
	client.Start();
	client.TryConnect();
	
```
### Server example configuration
```csharp
        HostOptions options = new HostOptions()
        {
            maxConnections = 20,
            timeOut = 30000,
            timePing = 1000,
            reliablePort = 7777,
            noReliablePort = 7778,
            windowSize = 1000,
            timerTime = 200,
            reliablePercentage = 80,
            messageHandler =  new MessageHandler(),
            waitTime = 1
        };
	bool reliable = true;
	options.messageHandler.AddHandler(1, (message,source)=>{
		Console.WriteLine("Message Received from client:" + ASCIIEncoding.GetString(m.data));
		server.SendToAll(1, reliable, null);
		server.SendTo(2,reliable,source,null);
	});
	bool useProcessingThread = true;
	Server server = new Server(options, useProcessingThread);
	//Start
	server.Start();
	server.AddPingHandler((m, s) => Debug.Log("Ping!"));
        server.onEndReceived += () => Debug.Log("End received");
        server.onSendEnd += () => Debug.Log("Sended an End");

```
