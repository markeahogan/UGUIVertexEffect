using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PopupAsylum.UIEffects
{
    public class GradientUIEffect : UIEffect
    {
        [SerializeField]
        GradientSource _source;

        GradientSource _subscribedSource;

        public GradientSource Source
        {
            get => _source;
            set 
            {
                _source = value;
                SetSubscribedSource(value);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetSubscribedSource(Source);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetSubscribedSource(null);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetSubscribedSource(Source);
        }

        public override void AddDivisions(RectTransform graphicTransform, List<Plane> list)
        {
            if (!_source) return;
            base.AddDivisions(graphicTransform, list);
            _source.AddDivisions(graphicTransform, list);
        }

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            if (!_source) return;
            _source.ModifyVertex(graphicTransform, ref vertex);
        }

        private void SetSubscribedSource(GradientSource source)
        {
            if (_subscribedSource) _subscribedSource.OnChanged -= MarkAsDirty;
            _subscribedSource = source;
            if (_subscribedSource) _subscribedSource.OnChanged += MarkAsDirty;
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            if (!_source) return;
            _source.ModifyVertices(graphicTransform, verts);
        }
    }
}