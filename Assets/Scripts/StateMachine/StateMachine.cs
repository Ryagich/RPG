using StateMachine.Graph;
using StateMachine.Graph.Model;

namespace StateMachine
{
    public class StateMachine
    {
        private readonly StateMachineGraph graph;
        private readonly StateMachineContext context;

        public StateMachine(StateMachineGraph graph, StateMachineContext context)
        {
            this.graph = graph;
            this.context = context ?? new StateMachineContext();
        }

        public State CurrentState { get; private set; }

        public void Start()
        {
            SetState(graph != null ? graph.GetEntryState() : null);
        }

        public void Tick(float deltaTime)
        {
            if (CurrentState == null)
            {
                return;
            }

            context.DeltaTime = deltaTime;
            context.ElapsedTime += deltaTime;

            if (CurrentState.Behaviours != null)
            {
                foreach (BaseBehaviour behaviour in CurrentState.Behaviours)
                {
                    behaviour?.Logic(context);
                }
            }

            if (CurrentState.Transitions == null)
            {
                return;
            }

            foreach (Transition transition in CurrentState.Transitions)
            {
                if (transition == null || !transition.CanTransition(context))
                {
                    continue;
                }

                if (transition.ActionOnTransitions != null)
                {
                    foreach (ActionOnTransitionBase action in transition.ActionOnTransitions)
                    {
                        action?.DoAction(context);
                    }
                }

                SetState(transition.TargetState);
                return;
            }
        }

        public void SetState(State state)
        {
            if (CurrentState == state)
            {
                return;
            }

            if (CurrentState?.Behaviours != null)
            {
                foreach (BaseBehaviour behaviour in CurrentState.Behaviours)
                {
                    behaviour?.Exit(context);
                }
            }

            CurrentState = state;

            if (CurrentState == null)
            {
                return;
            }

            if (CurrentState.Behaviours != null)
            {
                foreach (BaseBehaviour behaviour in CurrentState.Behaviours)
                {
                    behaviour?.Enter(context);
                }
            }

            if (CurrentState.Transitions == null)
            {
                return;
            }

            foreach (Transition transition in CurrentState.Transitions)
            {
                if (transition?.Conditions == null)
                {
                    continue;
                }

                foreach (BaseCondition condition in transition.Conditions)
                {
                    condition?.Enter(context);
                }
            }
        }
    }
}
