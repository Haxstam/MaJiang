using LiteNetLib;
using MaJiangLib;
using MaJiangLib.SignalClass;
using MaJiangLib.Utility;
public static class TestProgram
{
  public static void _Main()
  {
     string v= Console.ReadLine();
     if (v.Equals("C"))
     {
       new TestC().main();
     }
     if (v.Equals("S"))
     {
        new TestS().main();
     }
  }
  public class TestC
  {
      ISignalNetConnectHelper  helper;
      private EventBasedNetListener listener ;
      private NetManager client;
      private ISignalFactory factory;
      private NetPeer Serviver;
      public TestC()
      {
          factory = SignalFactory.Instance;
          listener = new EventBasedNetListener();
          client = new NetManager(listener);
      }
      private void ReCall(ISignalNetConnectHelper helper,GameNetSignal obj)
      {
        var  props =obj.GetType().GetProperties();
        foreach (var pp in props)
        {
            var pro=pp.GetValue(obj);
            Console.WriteLine(pp.Name+"  "+pro);
        }
        Console.WriteLine("\n");
      }
      public void main()
      {
          client.Start();
          client.UpdateTime = 15;
          Console.WriteLine("writeAddress");
          string  address =Console.ReadLine();
          Console.WriteLine("writePort");
          int  port = int.Parse(Console.ReadLine());
          Console.WriteLine("writeName");
          string  name =Console.ReadLine();
         NetPeer peer= client.Connect(address, port , name);
          helper=new SignalNetConnectHelper().Connect(peer);
          helper.OnSignalGet += ReCall;
          listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod, channel) =>
          {
              helper.SignalGet(dataReader);
          };
          while (true)
          {
              client.PollEvents();
              ushort  singlID = ushort.Parse(Console.ReadLine());
              helper.SignalWrite(factory.CreateEmptySignal(singlID));
              Console.WriteLine("write");
              helper.Send();
              Console.WriteLine("send");
              helper.Clear();
              Console.WriteLine("clear");
          }
      }
  }
  public class TestS
  {
      private EventBasedNetListener listener ;
      private NetManager Service;
      Dictionary<int,ISignalNetConnectHelper> clients;
      public TestS()
      {
          listener = new EventBasedNetListener();
          Service = new NetManager(listener);
          clients = new Dictionary<int, ISignalNetConnectHelper>();
      }
      private void Connect(NetPeer peer)
      {
          var helper=new SignalNetConnectHelper().Connect( peer);
          Console.WriteLine("Connect");
          clients.Add(peer.Id,helper);
          helper.OnSignalGet += CallBack;
      }
      private void CallBack(ISignalNetConnectHelper arg1, GameNetSignal arg2)
      {
          Console.WriteLine("GetMassage");
          var props = arg2.GetType().GetProperties();
          foreach (var pp in props)
          {
              var pro=pp.GetValue(arg2);
              Console.WriteLine(pp.Name+"  "+pro);
          }
      }
        public void main()
        {
            Console.WriteLine("writePort");
            int port = int.Parse(Console.ReadLine());
            Service.Start(port);
            listener.ConnectionRequestEvent += RequestEvent;
            listener.PeerConnectedEvent += Connect;
            Service.BroadcastReceiveEnabled = true;
            Service.UpdateTime = 15;
            listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod, channel) =>
            {
                if (!clients.TryGetValue(fromPeer.Id, out var helper)) return;
                helper.SignalGet(dataReader);
            };
            while (!Console.KeyAvailable)
            {
                Service.PollEvents();
                Thread.Sleep(15);
            }
        }
        
      private void RequestEvent(ConnectionRequest request)
      {
         string va=  request.Data.GetString();
         Console.WriteLine($"{va} : 尝试连接");
         request.Accept();
      }
  }
}