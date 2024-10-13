using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Reflection;
using UnityEngine.Profiling;

namespace PopupAsylum.UIEffects
{
    public partial class UIMeshModifier : IRaycasterStageCallback, ICanvasRaycastFilter
    {
        private static Vector3[] _corners = new Vector3[4];
        private Vector4 _padding;

        private void UpdateGraphicRaycasterCallback()
        {
            GraphicRaycasterCallbacks.Callbacks.Remove(this);
            bool isRoot = isActiveAndEnabled && ParentRef == null;
            if (isRoot)
            {
                GraphicRaycasterCallbacks.Callbacks.Add(this);
            }
        }

        void IRaycasterStageCallback.OnGraphicRaycaster(GraphicRaycasterCallbacks.Stage method, PointerEventData eventData, List<RaycastResult> results)
        {
            if (method == GraphicRaycasterCallbacks.Stage.Before)
            {
                ChangePadding();
            }
            else if (method == GraphicRaycasterCallbacks.Stage.After)
            {
                RestorePadding();
            }
        }

        /// <summary>
        /// Increases the padding of the graphic to infinity so that GraphicRaycaster's RectangleContainsScreenPoint always returns true
        /// </summary>
        void ChangePadding()
        {
            if (_isGraphic && EffectsModifyPosition)
            {
                _padding = _graphic.raycastPadding;
                _graphic.raycastPadding = Vector4.one * -10000;// Vector4.negativeInfinity; //TODO calculate just enough padding to capture the visual
                //TODO raycats padding can't help if the ray is pointing away from the canvas
            }
            _children.ForEach(x => x.ChangePadding());
        }

        void RestorePadding()
        {
            if (_isGraphic && EffectsModifyPosition) _graphic.raycastPadding = _padding;
            _children.ForEach(x => x.RestorePadding());
        }

        bool ICanvasRaycastFilter.IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (eventCamera == null)
            {
                ICanvasRaycastFilterCallbacks.SetCurrentGraphicFiltered();
                return true;
            }

            Profiler.BeginSample("UIMeshEffector.IsRaycastLocationValid");
            // So that children don't get clipped by thier parent rect (IsRaycastLocationValid is recursive up the hierarchy)
            if (ICanvasRaycastFilterCallbacks.CurrentGraphicHasBeenFiltered) { Profiler.EndSample(); return true; }

            // Save the cameras pose, since it may be modifed, to restore it after filtering finishes
            var cameraTransform = eventCamera.transform;
            var cameraPose = new Pose(cameraTransform.position, cameraTransform.rotation);
            ICanvasRaycastFilterCallbacks.SetCurrentGraphicFiltered(() => cameraTransform.SetPositionAndRotation(cameraPose.position, cameraPose.rotation));

            // If the graphic is not displaced then IsRaycastLocationValid should have only gotten called with a valid screenPoint
            // Even though this early return looks like it could be done earlier, it's called after SetCurrentGraphicFiltered so that this graphic gets marked as filtered
            if (!EffectsModifyPosition) { Profiler.EndSample(); return true; }

            // The graphic has been displaced (e.g. Bend modifier), the raycast is testing the wrong location.
            // To best support RectMask2D, rather than temporarily move the graphic to its new location we figure out where the ray hits on the visual,
            // then position the eventCamera so that the ray hits the equivalent point on the rectTransform
            GetWorldSpaceCorners(_corners);
            var pose = PoseFromCorners(_corners, (transform as RectTransform).pivot, out var _); //TODO PoseFromCorners could be overkill? we only need the rotation + distance for the Plane
            //if (!valid) { Profiler.EndSample(); return false; }

            var ray = eventCamera.ScreenPointToRay(screenPoint);
            var plane = new Plane(-pose.forward, pose.position);
            var hit = plane.Raycast(ray, out float enter);
            if (!hit) { Profiler.EndSample(); return false; } // ray missed the plane

            // find the bilinear coords for the hit
            var hitPoint = ray.GetPoint(enter);
            //Debug.DrawLine(cameraTransform.position, hitPoint, Color.blue);

            var p = GetInverseTransformedByXY(hitPoint, pose);
            var a = GetInverseTransformedByXY(_corners[1], pose);
            var b = GetInverseTransformedByXY(_corners[2], pose);
            var c = GetInverseTransformedByXY(_corners[3], pose);
            var d = GetInverseTransformedByXY(_corners[0], pose);
            var normalized = InverseBilinear(p, a, b, c, d);
            if (float.IsNaN(normalized.x) || float.IsNaN(normalized.y)) { Profiler.EndSample(); return false; }
            if (normalized.x < 0 || normalized.x > 1 || normalized.y < 0 || normalized.y > 1) { Profiler.EndSample(); return false; } // TODO account for _padding

            // convert the bilinear coords into the worldspace coords on the original rect
            RectTransform rect = transform as RectTransform;
            rect.GetWorldCorners(_corners);
            var ab = Vector3.LerpUnclamped(_corners[1], _corners[2], normalized.x);
            var dc = Vector3.LerpUnclamped(_corners[0], _corners[3], normalized.x);
            var x = Vector3.LerpUnclamped(ab, dc, normalized.y);
            //var x = Bilerp(_corners, normalized);

            // Position the camera so its ray hits where it hit on the modified rect, but on the original rect,
            // so that the camera is in the right place for subsequent ICanvasRaycastFilters e.g. RectMast2D
            // The camera pose gets restored at the end of ICanvasRaycastFilterCallbacks

            var rotationOffset = rect.rotation * Quaternion.Inverse(pose.rotation);
            var newRayDirection = rotationOffset * ray.direction;
            Vector3 pos = x - newRayDirection * enter;
            cameraTransform.SetPositionAndRotation(pos, cameraPose.rotation * rotationOffset);

            // Debugging
            var valid = RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, eventCamera, _padding);
            RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, screenPoint, eventCamera, out var worldPoint);
            Debug.DrawLine(cameraTransform.position, worldPoint, Color.green);

            Profiler.EndSample();
            return true;
        }

        static Vector3 Bilerp(Vector3[] corners, Vector2 t)
        {
            var inverseT = Vector2.one - t;
            return corners[1] * inverseT.x * inverseT.y +
                corners[2] * t.x * inverseT.y +
                corners[0] * inverseT.x * t.y +
                corners[3] * t.x * t.y;
        }

        /// <summary>
        /// Fills the given array with the modified corners of the Graphic, in world space
        /// </summary>
        public void GetWorldSpaceCorners(Vector3[] corners)
        {
            if (!_isGraphic) { return; }

            _graphic.rectTransform.GetLocalCorners(corners);

            for (int i = 0; i < 4; i++)
            {
                UIVertex vert = new UIVertex() { position = corners[i] };
                GetFinalPosition(this, ref vert);
                corners[i] = transform.TransformPoint(vert.position);
            }
        }

        /// <summary>
        /// Uses the left to right and top to bottom vectors between the corners to construct a pose
        /// </summary>
        private Pose PoseFromCorners(Vector3[] corners, Vector2 pivot) => PoseFromCorners(corners, pivot, out var _);
        private Pose PoseFromCorners(Vector3[] corners, Vector2 pivot, out bool valid)
        {
            // create a pose from theWS corners
            var fwd1 = -new Plane(corners[0], corners[1], corners[2]).normal;
            var fwd2 = -new Plane(corners[2], corners[3], corners[0]).normal;
            var fwd = (fwd1 + fwd2).normalized;
            if (fwd == Vector3.zero) { valid = false; return new Pose(transform.position, transform.rotation); }

            var center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
            var topCenter = (corners[1] + corners[2]) * 0.5f;
            var bottomCenter = (corners[0] + corners[3]) * 0.5f;
            var leftCenter = (corners[0] + corners[1]) * 0.5f;
            var rightCenter = (corners[3] + corners[2]) * 0.5f;

            var leftRight = rightCenter - leftCenter;
            var upDown = topCenter - bottomCenter;

            float leftRightMagnitude = leftRight.magnitude;
            float upDownMagnitude = upDown.magnitude;

            // we want the best fit of the the rect over the shape
            // in the case of a vertical skew the direction y axis is unchanged but the x changes dramatically https://graphicdesign.stackexchange.com/tags/skew/info
            // to rotate the rect to best fit we must determine which axis is most affected
            var upDownWeight = leftRightMagnitude / (leftRightMagnitude + upDownMagnitude);

            var leftRightUp = Vector3.Cross(fwd, leftRight);

            var up = Vector3.Lerp(upDown / upDownMagnitude, leftRightUp / leftRightMagnitude, upDownWeight);

            Quaternion rotation = Quaternion.LookRotation(fwd, up);
            var pivotCenter = center + 
                rotation * Vector3.up * (pivot.y-0.5f) * upDownMagnitude + 
                rotation * Vector3.right * (pivot.x-0.5f) * leftRightMagnitude;

            var raycastPose = new Pose(pivotCenter, rotation);
            valid = true;
            return raycastPose;
        }

        public Bounds CalculateWorldSpaceBounds()
        {
            GetWorldSpaceCorners(_corners);

            var bounds = new Bounds(_corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++)
            {
                bounds.Encapsulate(_corners[i]);
            }
            return bounds;
        }

        public Pose CalculateWorldSpacePose()
        {
            GetWorldSpaceCorners(_corners);
            return PoseFromCorners(_corners, (transform as RectTransform).pivot);
        }

        public static Pose GetInverseTransformedBy(Pose pose, Pose parent)
        {
            return new Pose
            {
                position = GetInverseTransformedBy(pose.position, parent),
                rotation = Quaternion.Inverse(parent.rotation) * pose.rotation
            };
        }

        public static Vector3 GetInverseTransformedBy(Vector3 position, Pose parent)
        {
            return Quaternion.Inverse(parent.rotation) * (position - parent.position);
        }

        public static Pose GetTransformedBy(Pose pose, Pose parent)
        {
            return new Pose
            {
                position = parent.position + parent.rotation * pose.position,
                rotation = parent.rotation * pose.rotation
            };
        }

        public static Vector2 GetInverseTransformedByXY(Vector3 position, Pose parent)
        {
            var v3 = GetInverseTransformedBy(position, parent);
            return new Vector2(v3.x, v3.y);
        }

        // https://iquilezles.org/articles/ibilinear/
        Vector2 InverseBilinear(in Vector2 p, in Vector2 a, in Vector2 b, in Vector2 c, in Vector2 d)
        {
            float cross2d(in Vector2 a, in Vector2 b) { return a.x * b.y - a.y * b.x; }

            Vector2 e = b - a;
            Vector2 f = d - a;
            Vector2 g = a - b + c - d;
            Vector2 h = p - a;

            float k2 = cross2d(g, f);
            float k1 = cross2d(e, f) + cross2d(h, g);
            float k0 = cross2d(h, e);

            // if edges are parallel, this is a linear equation
            if (Mathf.Abs(k2) < 0.001)
            {
                return new Vector2((h.x * k1 + f.x * k0) / (e.x * k1 - g.x * k0), -k0 / k1);
            }
            // otherwise, it's a quadratic
            else
            {
                float w = k1 * k1 - 4.0f * k0 * k2;
                if (w < 0.0) return new Vector2(-1, -1);
                w = Mathf.Sqrt(w);

                float ik2 = 0.5f / k2;
                float v = (-k1 - w) * ik2;
                float u = (h.x - f.x * v) / (e.x + g.x * v);

                if (u < 0.0 || u > 1.0 || v < 0.0 || v > 1.0)
                {
                    //v = (-k1 + w) * ik2;
                    //u = (h.x - f.x * v) / (e.x + g.x * v);
                }
                return new Vector2(u, v);
            }
        }

        internal bool PassesFilter(UIEffect effect) => _filters.TrueForAll(x => x.PassesFilter(effect));
    }

    /// <summary>
    /// UGUIMeshEffector leverages ICanvasRaycastFilter to limit raycasts to the Graphics modified position
    /// ICanvasRaycastFilter gets called on the Graphic but the also it's parents, the parent calls should be ignored as the parent may be in a different place to the child 
    /// e.g. a tooltip that is a child of button, positioned above the button
    /// ICanvasRaycastFilterCallbacks.CurrentGraphicHasBeenFiltered should be set true when ICanvasRaycastFilter is called on the Graphic itself
    /// While ICanvasRaycastFilterCallbacks.CurrentGraphicHasBeenFiltered is true we should assume its a parent and not perform further filtering (i.e. filters return true)
    /// </summary>
    internal static class ICanvasRaycastFilterCallbacks
    {
        public static bool CurrentGraphicHasBeenFiltered;

        static int _poolCount;
        static int _poolCountWhenFilteringStarted;
        static Action _onFinish;

        [RuntimeInitializeOnLoadMethod]
        private static void RegisterListeners()
        {
            // theres no real entry point for when a graphic will start raycast filtering
            // however in UGUI source Graphic.Raycast uses ListPool<Component>.Get() and Release() before and after filtering
            // ListPool<> has callbacks on Get and Release, we can hook into those
            // see https://github.com/Unity-Technologies/uGUI/blob/2019.1/UnityEngine.UI/UI/Core/Graphic.cs#L765
#if UNITY_2021_3_OR_NEWER
            var listPoolType = typeof(UnityEngine.Pool.ListPool<Component>);
            var s_PoolField = listPoolType.BaseType.GetField("s_Pool", BindingFlags.NonPublic | BindingFlags.Static);
            var pool = new UnityEngine.Pool.ObjectPool<List<Component>>(() => new List<Component>(), OnGet, l => { l.Clear(); OnRelease(l); });
            s_PoolField.SetValue(null, pool);
#else
            var listPoolType = Array.Find(typeof(Graphic).Assembly.GetTypes(), x => x.Name.Contains("ListPool")).MakeGenericType(typeof(Component));
            var s_ListPool = listPoolType.GetField("s_ListPool", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var objectPoolType = s_ListPool.GetType();

            RegisterListener("m_ActionOnGet", OnGet);
            RegisterListener("m_ActionOnRelease", OnRelease);

            void RegisterListener(string name, UnityAction<List<Component>> action)
            {
                var field = objectPoolType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                var value = (UnityAction<List<Component>>)field.GetValue(s_ListPool);
                value -= action; // probably not nessecary
                value += action;
                field.SetValue(s_ListPool, value);
            }
#endif
        }

        /// <summary>
        /// Called before ICanvasRaycastFilter (and in many other places)
        /// </summary>
        private static void OnGet(List<Component> _)
        {
            _poolCount++;
        }

        /// <summary>
        /// Called by ICanvasRaycastFilter, CurrentGraphicHasBeenFiltered will be set true until the pool used by the Graphic is released
        /// </summary>
        public static void SetCurrentGraphicFiltered(Action onFinish = null)
        {
            CurrentGraphicHasBeenFiltered = true;
            _poolCountWhenFilteringStarted = _poolCount;
            _onFinish = onFinish;
        }

        /// <summary>
        /// Called after ICanvasRaycastFilter (and in many other places)
        /// </summary>
        private static void OnRelease(List<Component> _)
        {
            // _poolCount can increase during ICanvasRaycastFilter,
            // if the pool count has returned to the value it was when filtering started then filtering is complete
            if (CurrentGraphicHasBeenFiltered && _poolCount == _poolCountWhenFilteringStarted)
            {
                CurrentGraphicHasBeenFiltered = false;
                _poolCountWhenFilteringStarted = -1;
                _onFinish?.Invoke();
            }
            _poolCount--;
        }
    }
}