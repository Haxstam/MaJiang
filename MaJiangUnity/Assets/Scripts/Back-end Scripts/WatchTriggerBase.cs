using System;
/// <summary>
/// 监听型委托触发器,通过提供值获取方式和判断方式,当目标满足时触发
/// </summary>
/// <typeparam name="ReturnType"></typeparam>
public class WatchTriggerBase<ReturnType, TargetType> : TriggerBase<ReturnType>
{
    public WatchTriggerBase(int id, Action<ReturnType> targetMethod, ReturnType result, Func<TargetType> getter, Func<TargetType, bool> condition, bool isSingle, bool isRunning = true) : base(id, targetMethod, result, isSingle, isRunning)
    {
        PropertyGetter = getter;
        Condition = condition;
    }
    /// <summary>
    /// 要监视的变量
    /// </summary>
    public Func<TargetType> PropertyGetter { get; set; }
    /// <summary>
    /// 监视条件
    /// </summary>
    public Func<TargetType, bool> Condition { get; set; }
    /// <summary>
    /// 检查方法调用,若满足条件且正在运行则调用触发器方法
    /// </summary>
    /// <param name="isActive">若成功触发则返回true</param>
    public virtual void OnDecide(out bool isActive)
    {
        isActive = false;
        if (IsRunning && Condition(PropertyGetter.Invoke()))
        {
            isActive = true;
            OnActive(ReturnResult);
        }
    }
}