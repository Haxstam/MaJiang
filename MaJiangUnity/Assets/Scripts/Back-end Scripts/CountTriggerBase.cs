using System;
/// <summary>
/// 计数型触发器,若达到目标值则返回
/// </summary>
/// <typeparam name="ReturnType">返回类型</typeparam>
/// <typeparam name="CountType">计数值的类型</typeparam>
public abstract class CountTriggerBase<ReturnType, CountType> : TriggerBase<ReturnType>
{
    public CountTriggerBase(int id, Action<ReturnType> targetMethod, ReturnType returnResult, CountType targetCount, bool isSingle, bool isRunning = true) : base(id, targetMethod, returnResult, isSingle, isRunning)
    {
        TargetCount = targetCount;
        OnReset();
        IsRunning = true;
    }
    /// <summary>
    /// 目标值
    /// </summary>
    public CountType TargetCount { get; set; }
    /// <summary>
    /// 当前计数值
    /// </summary>
    public CountType Count { get; set; }
    /// <summary>
    /// 当前计数值是否满足目标条件
    /// </summary>
    /// <returns></returns>
    public abstract bool IsSatisfied();
    /// <summary>
    /// 计数值更新
    /// </summary>
    /// <param name="add"></param>
    public abstract void OnUpdate(CountType add);
    /// <summary>
    /// 计数值重置
    /// </summary>
    public abstract void OnReset();
    /// <summary>
    /// 判断是否达到条件,如果达到则触发
    /// </summary>
    /// <param name="isActive">若成功触发则返回true</param>
    public virtual void OnDecide(out bool isActive)
    {
        isActive = false;
        if (IsSatisfied())
        {
            OnActive(ReturnResult);
            isActive = true;
        }
    }
}

