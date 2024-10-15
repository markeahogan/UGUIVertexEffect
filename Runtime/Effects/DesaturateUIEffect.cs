using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PopupAsylum.UIEffects
{
    public class DesaturateUIEffect : UIEffect
    {
        [SerializeField, Range(0, 1)]
        float _desaturation = 1;

        public override bool UsesShader => true;

        public float Desautration
        {
            get => _desaturation;
            set
            {
                _desaturation = value;
                MarkAsDirty();
            }
        }

        public override void ModifyVertex(RectTransform graphicTransform, ref UIVertex vertex)
        {
            if (_desaturation == 0) return;

            if (IsUGUIVertexEffectShader)
            {
                vertex.uv0.z = _desaturation;
            }
            else
            {
                vertex.color = Desaturate(vertex.color);
            }
        }

        private Color32 Desaturate(Color32 color)
        {
            byte lunimance = (byte)(color.r * 0.22f + color.g * 0.707f + color.b * 0.071f);
            return Color32.Lerp(color, new Color32(lunimance, lunimance, lunimance, color.a), _desaturation);
        }

        protected override void ModifyVertices(RectTransform graphicTransform, List<UIVertex> verts)
        {
            if (_desaturation == 0) return;

            if (IsUGUIVertexEffectShader)
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    var vert = verts[i];
                    vert.uv0.z = _desaturation;
                    verts[i] = vert;
                }
            }
            else
            {
                for (int i = 0; i < verts.Count; i++)
                {
                    var vert = verts[i];
                    vert.color = Desaturate(vert.color);
                    verts[i] = vert;
                }
            }
        }
    }
}
