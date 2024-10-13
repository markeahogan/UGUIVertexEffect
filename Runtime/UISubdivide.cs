using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PopupAsylum.UIEffects
{
    /// <summary>
    /// A component for subdividing UI outside of the UIEffect/UIMeshModifier system
    /// Used to make sure UIDivider stays independent from these systems
    /// </summary>
    public class UISubdivide : BaseMeshEffect
    {
        [SerializeField]
        private float _horizontalDivisions = 10;
        [SerializeField]
        private float _verticalDivisions = 10;
        [SerializeField]
        private UIDivider.Flags _dividerHints;
        [SerializeField]
        private bool _cacheResults = true;

        private bool _cached;
        private UIDivider _divider = new UIDivider();
        private List<int> _indices = new List<int>();
        private List<UIVertex> _verts = new List<UIVertex>();

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            _cached = false;
            base.OnValidate();
        }
#endif

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!isActiveAndEnabled) { return; }

            if (!_cached)
            {
                UpdateCache(vh);
            }

            if (_verts.Count > 0)
            {
                vh.Clear();
                vh.AddUIVertexStream(_verts, _indices);
            }
        }

        private void UpdateCache(VertexHelper vh)
        {
            List<Plane> planes = new List<Plane>();

            var rectTransform = graphic.rectTransform;
            var size = new Vector2(rectTransform.rect.width, rectTransform.rect.height);
            var origin = -rectTransform.pivot * size;

            if (_horizontalDivisions > 0)
            {
                var hd = _horizontalDivisions + 1;
                for (int i = 1; i < hd; i++)
                {
                    var n = (size.x / hd) * i;
                    var plane = new Plane(Vector3.right, origin.x + n);
                    planes.Add(plane);
                }
            }

            if (_verticalDivisions > 0)
            {
                var vd = _verticalDivisions + 1;
                for (int i = 1; i < vd; i++)
                {
                    var n = (size.y / vd) * i;
                    var plane = new Plane(Vector3.up, origin.y + n);
                    planes.Add(plane);
                }
            }

            if (planes.Count > 0)
            {
                _divider.Start(vh, _dividerHints, graphic.canvas.additionalShaderChannels);
                _divider.Divide(planes);
                _divider.Finish(_verts, _indices);
            }

            _cached = _cacheResults;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _cached = false;
        }
    }
}