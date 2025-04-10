using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using System.Threading;
using System;
using UnityEngine.Events;



namespace Modules.Utilities
{

    [DisallowMultipleComponent]
    public class StateMachineTriggerAsync : StateMachineBehaviour
    {


        public class OnStateInfo
        {
            public Animator Animator { get; private set; }
            public AnimatorStateInfo StateInfo { get; private set; }
            public int LayerIndex { get; private set; }

            public OnStateInfo(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
            {
                Animator = animator;
                StateInfo = stateInfo;
                LayerIndex = layerIndex;
            }
        }

        public class OnStateMachineInfo
        {
            public Animator Animator { get; private set; }
            public int StateMachinePathHash { get; private set; }

            public OnStateMachineInfo(Animator animator, int stateMachinePathHash)
            {
                Animator = animator;
                StateMachinePathHash = stateMachinePathHash;
            }
        }

        // OnStateEnter
        UnityEvent<OnStateInfo> onStateEnter;
        
        public IUniTaskAsyncEnumerable<OnStateInfo> OnStateEnterAsyncEnumerable(CancellationToken _token)
        {
            if (onStateEnter == null) onStateEnter = new UnityEvent<OnStateInfo>();
            return new UnityEventHandlerAsyncEnumerable<OnStateInfo>(onStateEnter, _token);
        }
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (onStateEnter != null) onStateEnter.Invoke(new OnStateInfo(animator, stateInfo, layerIndex));
        }

       //OnStateExit
        UnityEvent<OnStateInfo> onStateExit;
        public IUniTaskAsyncEnumerable<OnStateInfo> OnStateExitAsyncEnumerable(CancellationToken _token)
        {   
            if (onStateExit == null) onStateExit = new UnityEvent<OnStateInfo>();
            return new UnityEventHandlerAsyncEnumerable<OnStateInfo>(onStateExit, _token);
        }
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (onStateExit != null) onStateExit.Invoke(new OnStateInfo(animator, stateInfo, layerIndex));
        }

        // OnStateMachineEnter
        UnityEvent<OnStateMachineInfo> onStateMachineEnter;
        public IUniTaskAsyncEnumerable<OnStateMachineInfo> OnStateMachineEnterAsyncEnumerable(CancellationToken _token)
        {
            if (onStateMachineEnter == null) onStateMachineEnter = new UnityEvent<OnStateMachineInfo>();
            return new UnityEventHandlerAsyncEnumerable<OnStateMachineInfo>(onStateMachineEnter, _token);
        }
        public override void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
        {
            if (onStateMachineEnter != null) onStateMachineEnter.Invoke(new OnStateMachineInfo(animator, stateMachinePathHash));
        }

        // OnStateMachineExit
        UnityEvent<OnStateMachineInfo> onStateMachineExit;
        public IUniTaskAsyncEnumerable<OnStateMachineInfo> OnStateMachineExitAsyncEnumerable(CancellationToken _token)
        {
            if (onStateMachineExit == null) onStateMachineExit = new UnityEvent<OnStateMachineInfo>();
            return new UnityEventHandlerAsyncEnumerable<OnStateMachineInfo>(onStateMachineExit, _token);
        }
        public override void OnStateMachineExit(Animator animator, int stateMachinePathHash)
        {
            if (onStateMachineExit != null) onStateMachineExit.Invoke(new OnStateMachineInfo(animator, stateMachinePathHash));
        }

        // OnStateMove
        UnityEvent<OnStateInfo> onStateMove;
        public IUniTaskAsyncEnumerable<OnStateInfo> OnStateMoveAsyncEnumerable(CancellationToken _token)
        {
            if (onStateMove == null) onStateMove = new UnityEvent<OnStateInfo>();
            return new UnityEventHandlerAsyncEnumerable<OnStateInfo>(onStateMove, _token);
        }
        public override void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (onStateMove != null) onStateMove.Invoke(new OnStateInfo(animator, stateInfo, layerIndex));
        }

        // OnStateIK
        UnityEvent<OnStateInfo> onStateIK;
        public IUniTaskAsyncEnumerable<OnStateInfo> OnStateIKAsyncEnumerable(CancellationToken _token)
        {
            if (onStateIK == null) onStateIK = new UnityEvent<OnStateInfo>();
            return new UnityEventHandlerAsyncEnumerable<OnStateInfo>(onStateIK, _token);
        }

        public override void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (onStateIK != null) onStateIK.Invoke(new OnStateInfo(animator, stateInfo, layerIndex));
        }

    }
}