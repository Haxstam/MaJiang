
using LiteNetLib;
using MaJiangLib.SignalClass;
using MaJiangLib.Utility;
public static class TestProgram
{
  public static void Main()
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
      NetConnectHelper  helper;
      private EventBasedNetListener listener ;
      private NetManager client;
      private NetPeer Serviver;
      public TestC()
      {
          helper = new NetConnectHelper();
          listener = new EventBasedNetListener();
          client = new NetManager(listener);
          helper.OnSignalGet += ReCall;
      }
      private void ReCall(GameNetSignal obj)
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
          Console.WriteLine("writeAddress");
          string  address =Console.ReadLine();
          Console.WriteLine("writePort");
          int  port = int.Parse(Console.ReadLine());
          Console.WriteLine("writeName");
          string  name =Console.ReadLine();
          client.Connect(address, port , name);
          listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod, channel) =>
          {
              helper.GetSignal(dataReader);
          };
      }
  }
  public class TestS
  {
      NetConnectHelper  helper;
      public TestS()
      {
          helper = new NetConnectHelper();
      }
      public void main()
      {
          
      }
  }
}