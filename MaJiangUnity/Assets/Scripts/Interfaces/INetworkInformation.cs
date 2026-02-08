using System.Net;
using UnityEngine;

public interface INetworkInformation
{
    public IPAddress SelfIPAddress { get; set; }
    public UserProfile SelfProfile { get; set; }
    public int GetTaskID();
}
