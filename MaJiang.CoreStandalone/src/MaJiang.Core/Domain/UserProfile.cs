/// <summary>座位上的玩家显示信息，不包含连接或认证身份。</summary>
public sealed class UserProfile
{
    /// <summary>创建空的玩家显示信息。</summary>
    public UserProfile() { }
    /// <summary>创建指定名称的玩家显示信息。</summary>
    public UserProfile(string name) { Name = name; }
    /// <summary>该座位玩家的显示名称。</summary>
    public string Name { get; set; }
}
