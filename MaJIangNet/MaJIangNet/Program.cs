using System.Text;
using LiteNetLib;
using LiteNetLib.Utils;
public static class TestProgram
{
  public static void Main()
  {
     string v= Console.ReadLine();
     if (v.Equals("C"))
     {
         EventBasedNetListener listener = new EventBasedNetListener();
         NetManager client = new NetManager(listener);
         client.Start();
         NetPeer mainPeer = client.Connect("localhost" /* host IP or name */, 9050 /* port */, "SomeConnectionKey" /* text key or NetDataWriter */);
         listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod, channel) =>
         {
             Console.WriteLine("We got: {0}", dataReader.GetString(100 /* max length of string */));
             dataReader.Recycle();
         };
         string read=Console.ReadLine();
         while (!read.Equals("q"))
         {
                 NetDataWriter writer = new NetDataWriter();       
                 writer.Put(read);                     
                 mainPeer.Send(writer, DeliveryMethod.ReliableOrdered); 
                 read=Console.ReadLine();
         }
         client.Stop();


     }
     if (v.Equals("S"))
     {
         EventBasedNetListener listener = new EventBasedNetListener();
         NetManager server = new NetManager(listener);
         server.Start(9050 /* port */);

         listener.ConnectionRequestEvent += request =>
         {
             if(server.ConnectedPeersCount < 10 /* max connections */)
                 request.AcceptIfKey("SomeConnectionKey");
             else
                 request.Reject();
         };

         listener.PeerConnectedEvent += peer =>
         {
             Console.WriteLine("We got connection: {0}", peer);  // Show peer IP
             NetDataWriter writer = new NetDataWriter();         // Create writer class
             writer.Put("Hello client!");                        // Put some string
             peer.Send(writer, DeliveryMethod.ReliableOrdered);  // Send with reliability
         };
         
         listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod, channel) =>
         {
             Console.WriteLine("We got: {0}  from {1}", dataReader.GetString(100 /* max length of string */), fromPeer.Id);
             dataReader.Recycle();
         };
         while (!Console.KeyAvailable)
         {
             server.PollEvents();
           
             Thread.Sleep(15);
         }
         server.Stop();
     }
  }
}