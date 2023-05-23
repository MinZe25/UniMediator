using UnityEngine;

namespace UniMediator
{
    [RequireComponent(typeof(MediatorImpl))]
    [DefaultExecutionOrder(-1000)]
    public class Mediator : MonoBehaviour
    {
        private static IMediator _mediator;

        public void SetMediator(IMediator mediator)
        {
            _mediator = mediator;
        }

        protected virtual void Awake()
        {
            _mediator = GetComponent<MediatorImpl>();
        }

        public static void Publish(IMulticastMessage message)
        {
            _mediator.Publish(message);
        }

        public static T Send<T>(ISingleMessage<T> message)
        {
            return _mediator.Send(message);
        }

        public static void AddMediatedObject(MonoBehaviour monoBehaviour)
        {
            _mediator.AddMediatedObject(monoBehaviour);
        }
    }
}