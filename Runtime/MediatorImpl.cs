using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UniMediator.Internal;

namespace UniMediator
{
    public sealed class MediatorImpl : MonoBehaviour, IMediator
    {
        private readonly SingleMessageHandlerCache _singleMessageHandlers = new SingleMessageHandlerCache();

        private readonly MulticastMessageHandlerCache _multicastMessageHandlers = new MulticastMessageHandlerCache();

        private readonly ActiveObjectTracker _activeObjects = new ActiveObjectTracker();

        private static MediatorImpl _instance;

        private Scene _activeScene;

        private void Awake()
        {
            if (_instance is null)
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else if (_instance != this)
            {
                DestroyImmediate(this);
                return;
            }

            this._activeScene = SceneManager.GetActiveScene();
            ScanScene(this._activeScene);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Publish(IMulticastMessage message)
        {
            this._multicastMessageHandlers.Invoke(message);
        }

        public T Send<T>(ISingleMessage<T> message)
        {
            return this._singleMessageHandlers.Invoke(message);
        }

        public void AddMediatedObject(MonoBehaviour monoBehaviour)
        {
            ExtractHandlers(monoBehaviour);
        }

        private void ScanScene()
        {
            ScanScene(SceneManager.GetActiveScene());
        }

        private void ScanScene(Scene scene)
        {
            MonoBehaviour[] monoBehaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            foreach (MonoBehaviour t in monoBehaviours)
            {
                if (t.gameObject.scene == scene)
                {
                    ExtractHandlers(t);
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene != this._activeScene)
            {
                ScanScene(scene);
            }
        }

        private void ExtractHandlers(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();

            if (!type.ImplementsGenericInterface(typeof(IMulticastMessageHandler<>)) &&
                !type.ImplementsGenericInterface(typeof(ISingleMessageHandler<,>))) return;
            MethodInfo[] methods = type.GetCachedMethods();

            foreach (MethodInfo t in methods)
            {
                ParameterInfo[] parameters = t.GetCachedParameters();

                if (parameters.Length != 1) continue;

                Type messageType = parameters[0].ParameterType;

                if (typeof(IMulticastMessage).IsAssignableFrom(messageType))
                {
                    CacheMulticastMessageHandler(messageType, behaviour, t);
                }

                else if (messageType.ImplementsGenericInterface(typeof(ISingleMessage<>)))
                {
                    CacheSingletMessageHandler(messageType, behaviour, t);
                }
            }
        }

        private void CacheMulticastMessageHandler(Type messageType, MonoBehaviour behavior, MethodInfo method)
        {
            Action<IMulticastMessage> handler =
                this._multicastMessageHandlers.CacheHandler(messageType, behavior, method);
            var remover = new MulticastMessageHandlerRemover(messageType, handler, this._multicastMessageHandlers);
            AddLifeCycleMonitor(behavior.gameObject, remover);
        }

        private void CacheSingletMessageHandler(Type messageType, MonoBehaviour behavior, MethodInfo method)
        {
            Type returnType = messageType.GetInterfaces()[0].GenericTypeArguments[0];
            this._singleMessageHandlers.CacheHandler(messageType, returnType, behavior, method);
            var remover = new SingleMessageHandlerRemover(messageType, this._singleMessageHandlers);
            AddLifeCycleMonitor(behavior.gameObject, remover);
        }

        private void AddLifeCycleMonitor(GameObject @object, IDelegateRemover remover)
        {
            if (!this._activeObjects.Contains(@object))
            {
                MediatorLifecycleMonitor monitor = @object.AddComponent<MediatorLifecycleMonitor>();
                monitor.ActiveObjects = this._activeObjects;
            }

            this._activeObjects.AddActiveObject(@object, remover);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}