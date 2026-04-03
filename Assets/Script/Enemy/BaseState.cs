using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 状态脚本的基类
public abstract class BaseState
{
    protected AIStateMachine stateMachine;

    public BaseState(AIStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }

    public abstract void OnEnter();
    public abstract void OnUpdate();
    public abstract void OnExit();
}
