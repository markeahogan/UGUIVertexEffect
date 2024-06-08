using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;

namespace PopupAsylum.UIEffects
{
    /// <summary>
    /// Bends the UI around a cylinder
    /// </summary>
    public class BendUIEffect : UIEffect
    {
        private static Vector3[] _corners = new Vector3[4];

        [SerializeField]
        float _angle = 90;

        [SerializeField, Range(1f, 360f)]
        float _degreesPerDivision = 10;

        private float _radius;

        public override Space UIVertexSpace => Space.Local;
        public override bool AffectsPosition => true;

        /// <summary>
        /// The angle this rect transform should cover
        /// </summary>
        public float Angle
        {
            get => _angle;
            set
            {
                _angle = value;
                MarkAsDirty();
            }
        }

        /// <summary>
        /// Determines how many divisions will be added to smooth out the cylinder shape
        /// </summary>
        public float DegreesPerDivision
        {
            get => _degreesPerDivision; 
            set
            {
                _degreesPerDivision = value;
                MarkAsDirty();
            }
        }

        private void Update()
        {
            float width = (transform as RectTransform).rect.width;
            float circumference = (360 / _angle) * width;
            _radius = circumference / (2 * Mathf.PI);
        }

        public override void AddDivisions(RectTransform graphicTransform, List<Plane> list)
        {
            base.AddDivisions(graphicTransform, list);

            if (_angle == 0 || _degreesPerDivision < 1) { return; }

            float width = (transform as RectTransform).rect.width;
            float circumference = (360 / _angle) * width;
            float radius = circumference / (2 * Mathf.PI);
            float radiansPerDivision = _degreesPerDivision * Mathf.Deg2Rad;

            graphicTransform.GetWorldCorners(_corners);
            float xMin = float.MaxValue;
            float xMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float xPos = transform.InverseTransformPoint(_corners[i]).x / radius;
                xMin = Mathf.Min(xPos, xMin);
                xMax = Mathf.Max(xPos, xMax);
            }

            xMin = Mathf.Ceil(xMin / radiansPerDivision) * radiansPerDivision;

            for (float i = xMin; i < xMax; i += radiansPerDivision)
            {
                Plane plane = new Plane(Vector3.right, i * radius);
                ConvertSpace(ref plane, transform, graphicTransform);
                list.Add(plane);
            }
        }

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            Profiler.BeginSample("BendUGUIEffect.ModifyVertex");
            vertex = BendVertex(vertex);
            Profiler.EndSample();
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            Profiler.BeginSample("BendUGUIEffect.ModifyVertices");
            int count = verts.Count;
            for (int i = 0; i < count; i++)
            {
                verts[i] = BendVertex(verts[i]);
            }
            Profiler.EndSample();
        }

        private UIVertex BendVertex(UIVertex vertex)
        {
            if (_radius == 0) Update();
            if (_angle == 0 || _degreesPerDivision < 1 || _radius == 0) { return vertex; }

            float norm = vertex.position.x / _radius;

            float localRadius = _radius + vertex.position.z;
            vertex.position.x = Mathf.Sin(norm) * localRadius;
            vertex.position.z = -_radius + Mathf.Cos(norm) * localRadius;

            var rotation = Quaternion.Euler(0, norm * Mathf.Rad2Deg, 0);
            vertex.normal = rotation * vertex.normal;
            return vertex;
        }
    }
}