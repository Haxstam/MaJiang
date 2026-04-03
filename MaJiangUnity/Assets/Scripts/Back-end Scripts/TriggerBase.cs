using System;
using UnityEngine;
/// <summary>
/// 触发器基类
/// </summary>
/// <typeparam name="ReturnType">返回值类型</typeparam>
public abstract class TriggerBase<ReturnType>
{
    public TriggerBase(int id, Action<ReturnType> targetMethod, ReturnType result, bool isSingle, bool isRunning = true)
    {
        ID = id;
        TargetMethod = targetMethod;
        ReturnResult = result;
        IsRunning = isRunning;
        IsSingle = isSingle;
    }
    /// <summary>
    /// 计数器ID
    /// </summary>
    public int ID { get; set; }
    /// <summary>
    /// 将要触发的函数
    /// </summary>
    public Action<ReturnType> TargetMethod { get; set; }
    /// <summary>
    /// 触发的值
    /// </summary>
    public ReturnType ReturnResult { get; set; }
    /// <summary>
    /// 是否在运行
    /// </summary>
    public bool IsRunning { get; set; }
    /// <summary>
    /// 是否仅运行单次
    /// </summary>
    public bool IsSingle { get; set; }
    /// <summary>
    /// 触发函数的方法,不考虑是否运行或条件满足
    /// </summary>
    /// <param name="parameter"></param>
    public virtual void OnActive(ReturnType parameter)
    {
        TargetMethod.Invoke(parameter);
        if (IsSingle)
        {
            IsRunning = false;
        }
    }
}
