using PopupAsylum.UIEffects;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace PopupAsylum.UIEffects
{
    [ExecuteInEditMode]
    public abstract class UIEffect : MonoBehaviour
    {
        [SerializeField]
        protected Affect _affect = Affect.SelfAndChildren;

        protected readonly UGUIMeshEffectorReference effector;
        protected RectTransform rectTransform;

        private Matrix4x4 _graphicToLocal;
        private Matrix4x4 _localToGraphic;

        /// <summary>
        /// When set to Space.Graphic, the UIVertex's position and normal will be relative to the graphic. 
        /// When set to Space.Local the UIVertex's position and normal will be relative to the Effect, this is
        /// useful for bend and ripple effects.
        /// </summary>
        public virtual Space UIVertexSpace => Space.Graphic;

        /// <summary>
        /// Used to indicate that this effect modifies the vertices position, to aid raycasting
        /// </summary>
        public virtual bool AffectsPosition => false;

        /// <summary>
        /// Used to indicate that this effect needs to be run per vertex
        /// </summary>
        public virtual bool AffectsVertex => true;

        protected virtual void OnEnable()
        {
            rectTransform = transform as RectTransform;
            effector.SetEffectAdded(this, true);
        }

        protected virtual void OnDisable() => effector.SetEffectAdded(this, false);
        protected virtual void OnValidate() { if (isActiveAndEnabled) { effector.MarkAsDirty(this); } }

        /// <summary>
        /// Sets up the lis of divisions for the graphic, subclasses can override this
        /// </summary>
        public virtual void AddDivisions(RectTransform graphicTransform, List<Plane> list)
        {

        }

        /// <summary>
        /// Handles transforming the vertex into the rigth space before passing it to subclasses
        /// </summary>
        internal void InternalModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            Profiler.BeginSample("UGUIVertexEffect.InternalModifyVertex");
            bool local = UIVertexSpace == Space.Local;
            if (local) { GraphicToLocalSpace(ref vertex); }

            ModifyVertex(graphicTransform, ref vertex);

            if (local) { LocalToGraphicSpace(ref vertex); }
            Profiler.EndSample();
        }

        public abstract void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex);

        /// <summary>
        /// Outputs the bottom left corner and size of the rectTransform</br>
        /// Useful for normalizing a point
        /// </summary>
        protected void SizeAndOrigin(out Vector2 size, out Vector2 origin) => SizeAndOrigin(rectTransform, out size, out origin);

        /// <summary>
        /// Outputs the bottom left corner and size of the rectTransform</br>
        /// Useful for normalizing a point
        /// </summary>
        protected static void SizeAndOrigin(RectTransform rectTransform, out Vector2 size, out Vector2 origin)
        {
            size = new Vector2(rectTransform.rect.width, rectTransform.rect.height);
            origin = -rectTransform.pivot * size;
        }

        /// <summary>
        /// Converts the UIVertex into localspace, call LocalToGraphicSpace after making changes
        /// </summary>
        protected void GraphicToLocalSpace(ref UIVertex vertex)
        {
            vertex.position = _graphicToLocal.MultiplyPoint3x4(vertex.position);
            vertex.normal = _graphicToLocal.MultiplyVector(vertex.normal);
        }

        /// <summary>
        /// Converts the UIVertex back into graphic space
        /// </summary>
        protected void LocalToGraphicSpace(ref UIVertex vertex)
        {
            vertex.position = _localToGraphic.MultiplyPoint3x4(vertex.position);
            vertex.normal = _localToGraphic.MultiplyVector(vertex.normal);
        }

        public static void ConvertSpace(ref UIVertex vertex, Transform from, Transform to)
        {
            Profiler.BeginSample("UGUIVertexEffect.ConvertSpace");
            vertex.position = to.InverseTransformPoint(from.TransformPoint(vertex.position));
            vertex.normal = to.InverseTransformDirection(from.TransformDirection(vertex.normal));
            Profiler.EndSample();
        }

        /// <summary>
        /// Converts a plane from one transforms space to another.</br>
        /// Useful for UIDivider planes must bt supplied in the grapics local space
        /// </summary>
        public static void ConvertSpace(ref Plane plane, Transform from, Transform to, bool debug = false)
        {
            // Matrix4x4 localToLocal = to.worldToLocalMatrix * from.localToWorldMatrix;
            // plane = localToLocal.TransformPlane(plane); <--- why doesnt this work??

            // var p = to.InverseTransformPoint(from.TransformPoint(plane.normal * plane.distance));
            // var n = to.InverseTransformDirection(from.TransformDirection(plane.normal));
            // plane.distance = Vector3.Dot(p, n);
            // plane.normal = n;

            if (debug) DrawPlane(plane, from, Color.blue);

            var p = to.InverseTransformPoint(from.TransformPoint(plane.normal * plane.distance));
            plane.SetNormalAndPosition(to.InverseTransformDirection(from.TransformDirection(plane.normal)), p);

            if (debug) DrawPlane(plane, to, Color.green);
        }

        private static void DrawPlane(Plane plane, Transform transform, Color blue)
        {
            var pos = plane.distance * plane.normal;
            var wsPos = transform.TransformPoint(pos);
            var norm = plane.normal;
            var wsNorm = transform.TransformDirection(norm);
            var up = Vector3.up;
            var right = Vector3.zero;
            Vector3.OrthoNormalize(ref wsNorm, ref up, ref right);
            Debug.DrawLine(wsPos - up * 1000, wsPos + up * 1000, blue);
            Debug.DrawLine(wsPos - right * 1000, wsPos + right * 1000, blue);
            Debug.DrawLine(wsPos, wsPos + wsNorm * 10, blue);
        }

        internal void InternalAddDivisions(UIMeshModifier meshEffector, List<Plane> divisions)
        {
            if (!ShouldAffect(meshEffector)) { return; }

            AddDivisions(meshEffector.transform as RectTransform, divisions);
        }

        protected void MarkAsDirty() => effector.MarkAsDirty(this);

        private bool ShouldAffect(UIMeshModifier meshEffector)
        {
            bool isSelf = meshEffector.gameObject == gameObject;
            switch (_affect)
            {
                case Affect.Self: return isSelf && PassesFilter();
                case Affect.Children: return !isSelf && PassesFilter();
                case Affect.SelfAndChildren: return PassesFilter();
                default: throw new Exception();
            }

            bool PassesFilter() => meshEffector.PassesFilter(this);
        }

        public bool PreProcess(UIMeshModifier meshEffector)
        {
            if (!ShouldAffect(meshEffector)) return false;

            _graphicToLocal = rectTransform.worldToLocalMatrix * meshEffector.transform.localToWorldMatrix;
            _localToGraphic = _graphicToLocal.inverse;

            return true;
        }

        internal void InternalAddClippingPlanes(UIMeshModifier meshEffector, List<Plane> divisions)
        {
            if (!ShouldAffect(meshEffector)) { return; }

            AddClippingPlanes(meshEffector.transform as RectTransform, divisions);
        }

        protected virtual void AddClippingPlanes(RectTransform graphicTransform, List<Plane> divisions)
        {
        }

        internal void InternalModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            Profiler.BeginSample("UGUIVertexEffect.InternalModifyVertices");
            var count = verts.Count;
            bool local = UIVertexSpace == Space.Local;

            if (local)
            {
                Profiler.BeginSample("UGUIVertexEffect.InternalModifyVertices.GraphicToLocalSpace");
                for (int i = 0; i < count; i++)
                {
                    var vertex = verts[i];
                    GraphicToLocalSpace(ref vertex);
                    verts[i] = vertex;
                }
                Profiler.EndSample();
            }

            Profiler.BeginSample("UGUIVertexEffect.InternalModifyVertices.ModifyVertices");
            ModifyVertices(graphicTransform, verts);
            Profiler.EndSample();

            if (local)
            {
                Profiler.BeginSample("UGUIVertexEffect.InternalModifyVertices.LocalToGraphicSpace");
                for (int i = 0; i < count; i++)
                {
                    var vertex = verts[i];
                    LocalToGraphicSpace(ref vertex);
                    verts[i] = vertex;
                }
                Profiler.EndSample();
            }

            Profiler.EndSample();
        }

        protected abstract void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts);

        protected void GridSubdivide(RectTransform graphicTransform, List<Plane> list, Vector2Int divisions)
        {
            SizeAndOrigin(out var size, out var origin);

            if (divisions.x > 0)
            {
                var hd = divisions.x + 1;
                for (int i = 1; i < hd; i++)
                {
                    var n = (size.x / hd) * i;
                    var plane = new Plane(Vector3.right, origin.x + n);
                    ConvertSpace(ref plane, rectTransform, graphicTransform);
                    list.Add(plane);
                }
            }

            if (divisions.y > 0)
            {
                var vd = divisions.y + 1;
                for (int i = 1; i < vd; i++)
                {
                    var n = (size.y / vd) * i;
                    var plane = new Plane(Vector3.up, origin.y + n);
                    ConvertSpace(ref plane, rectTransform, graphicTransform);
                    list.Add(plane);
                }
            }
        }

        protected struct UGUIMeshEffectorReference
        {
            public UIMeshModifier _effector;
            bool _hasGetBeenCalled;

            public void SetEffectAdded(UIEffect effect, bool add)
            {
                EnsureAdded(effect);

                if (!_effector) { return; }

                _effector.SetEffectAdded(effect, add);
            }

            public void MarkAsDirty(UIEffect effect)
            {
                EnsureAdded(effect);

                if (!_effector) { return; }

                _effector.MarkAsDirty();
            }

            private void EnsureAdded(UIEffect effect)
            {
                if (!_hasGetBeenCalled || !Application.isPlaying)
                {
                    _hasGetBeenCalled = true;
                    _effector = UIMeshModifier.GetOrAdd(effect);
                }
            }
        }

        public enum Space
        {
            Graphic,
            Local
        }

        public enum Affect
        {
            Self,
            Children,
            SelfAndChildren
        }
    }
}