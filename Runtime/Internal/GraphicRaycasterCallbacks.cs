using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

#if XRTK_INCLUDED
using UnityEngine.XR.Interaction.Toolkit.UI;
#endif

namespace PopupAsylum.UIEffects
{
    public static class GraphicRaycasterCallbacks
    {
        public static event Action<Stage, PointerEventData, List<RaycastResult>> OnGraphicRaycast;
        public static readonly List<IRaycasterStageCallback> Callbacks = new List<IRaycasterStageCallback>();
        public static bool autoInvoke = true;

        [RuntimeInitializeOnLoadMethod]
        static void Initialize()
        {
            var before = new GameObject(nameof(BeforeRaycaster)).AddComponent<BeforeRaycaster>();
            before.invoke = InvokeBefore;
            UnityEngine.Object.DontDestroyOnLoad(before.gameObject);
            var after = new GameObject(nameof(AfterRaycaster)).AddComponent<AfterRaycaster>();
            after.invoke = InvokeAfter;
            UnityEngine.Object.DontDestroyOnLoad(after.gameObject);

            // some systems expect BaseRaycasters to have a canvas component
            before.gameObject.AddComponent<Canvas>().enabled = false;
            after.gameObject.AddComponent<Canvas>().enabled = false;
        }

        public static void SetAdded(IRaycasterStageCallback callback, bool add)
        {
            if (add)
            {
                Callbacks.Add(callback);
            }
            else
            {
                Callbacks.Remove(callback);
            }
        }

        public static void InvokeBefore(PointerEventData eventData, List<RaycastResult> results)
        {
            Invoke(eventData, results, Stage.Before);
        }

        public static void InvokeAfter(PointerEventData eventData, List<RaycastResult> results)
        {
            Invoke(eventData, results, Stage.After);
        }

        private static void Invoke(PointerEventData eventData, List<RaycastResult> results, Stage stage)
        {
            for (int i = 0; i < Callbacks.Count; i++)
            {
                Callbacks[i].OnGraphicRaycaster(stage, eventData, results);
            }
            OnGraphicRaycast?.Invoke(stage, eventData, results);
        }

        public enum Stage
        {
            Before,
            After
        }

        private abstract class RaycasterCallback : BaseRaycaster
        {
            protected static List<BaseRaycaster> Raycasters;

            public Action<PointerEventData, List<RaycastResult>> invoke;
            public override Camera eventCamera => null;

            protected override void Awake()
            {
                Raycasters ??= typeof(BaseRaycaster).Assembly
                  .GetType("UnityEngine.EventSystems.RaycasterManager")?
                  .GetField("s_Raycasters", BindingFlags.NonPublic | BindingFlags.Static)?
                  .GetValue(null) as List<BaseRaycaster>;

                base.Awake();
            }

            public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
            {
                if (autoInvoke)
                {
                    invoke?.Invoke(eventData, results);
                }
            }

            protected void SetEnabled(BaseRaycaster raycaster, bool value)
            {
                if (raycaster is RaycasterCallback) return;
                raycaster.enabled = value;
            }

#if XRTK_INCLUDED
            protected static Pose? _xrCameraPose = null;
#endif
        }

        private class BeforeRaycaster : RaycasterCallback
        {
            protected override void OnEnable()
            {
                Raycasters.Insert(0, this);
            }

            public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
            {
#if XRTK_INCLUDED
                if (eventData is TrackedDeviceEventData laser && laser.rayPoints.Count > 1 && !_xrCameraPose.HasValue)
                {
                    var mainCamera = Camera.main.transform;
                    _xrCameraPose = new Pose(mainCamera.position, mainCamera.rotation);

                    var last = laser.rayPoints.Count-1;
                    var rayPose = new Pose(laser.rayPoints[0], Quaternion.LookRotation(laser.rayPoints[last] - laser.rayPoints[0]));

                    mainCamera.SetPositionAndRotation(rayPose.position, rayPose.rotation);
                }
#endif
                base.Raycast(eventData, results);
            }
        }

        private class AfterRaycaster : RaycasterCallback
        {
            private void Update()
            {
                var index = Raycasters.LastIndexOf(this);
                var last = Raycasters.Count - 1;
                Raycasters[index] = Raycasters[last];
                Raycasters[last] = this;
            }

            public override void Raycast(PointerEventData eventData, List<RaycastResult> results)
            {
                base.Raycast(eventData, results);
#if XRTK_INCLUDED
                if (eventData is TrackedDeviceEventData && _xrCameraPose.HasValue)
                {
                    var mainCamera = Camera.main.transform;
                    mainCamera.SetPositionAndRotation(_xrCameraPose.Value.position, _xrCameraPose.Value.rotation);
                    _xrCameraPose = null;
                }
#endif
            }
        }
    }

    public interface IRaycasterStageCallback
    {
        void OnGraphicRaycaster(GraphicRaycasterCallbacks.Stage method, PointerEventData eventData, List<RaycastResult> results);
    }
}