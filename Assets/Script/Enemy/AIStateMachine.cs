using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 状态机控制调用脚本
public class AIStateMachine
{
    public BaseState currentState;

    public Enemy_Controller controller;

    // AI手动调用进行绑定
    public AIStateMachine(Enemy_Controller controller)
    {
        this.controller = controller;
    }

    // 状态切换
    public void ChangeState(BaseState newState)
    {
        if (newState == null || currentState == newState)
            return;

        if (currentState != null)
            currentState.OnExit();

        currentState = newState;
        currentState.OnEnter();
    }

    // 状态更新
    public void UpdateState()
    {
        currentState.OnUpdate();
    }
}
