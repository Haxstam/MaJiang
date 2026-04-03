using System;

public class FloatTriggerBase<ReturnType> : CountTriggerBase<ReturnType, float>
{
    /// <summary>
    /// float计数器的默认构造器
    /// </summary>
    /// <param name="id">计数器ID</param>
    /// <param name="targetMethod">将要触发的方法</param>
    /// <param name="returnResult">触发时返回结果</param>
    /// <param name="targetCount">触发目标值,默认为20f</param>
    public FloatTriggerBase(int id, Action<ReturnType> targetMethod, ReturnType returnResult, bool isSingle, float targetCount = 20f, bool isRunning = true) : base(id, targetMethod, returnResult, targetCount, isSingle, isRunning)
    {

    }

    public override bool IsSatisfied()
    {
        return IsRunning && Count >= TargetCount;
    }

    public override void OnReset()
    {
        Count = 0;
    }

    public override void OnUpdate(float add)
    {
        Count += add;
    }
}

