using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.TerrainAPI;

namespace UnityEditor.Experimental.TerrainAPI
{
    internal class StampToolExtended : TerrainPaintTool<StampToolExtended>
    {
        class Styles
        {
            public static readonly GUIContent stampHeight = EditorGUIUtility.TrTextContent("Stamp Height", "");
            public static readonly GUIContent invertStampHeight = EditorGUIUtility.TrTextContent("Invert Stamp Height", "");

            public static readonly GUIContent brushSize = EditorGUIUtility.TrTextContent("Brush Size", "");
            public static readonly GUIContent brushOpacity = EditorGUIUtility.TrTextContent("Opacity", "");
            public static readonly GUIContent brushRotation = EditorGUIUtility.TrTextContent("Rotation", "");
        }

        [SerializeField]
        private float m_StampHeight = 0.0f;

        [SerializeField]
        private float m_BrushRotation = 0.0f;

        [SerializeField]
        private float m_BrushSize = 40.0f;

        [SerializeField]
        private float m_BrushStrength = 1.0f;

        private const float brushSizeSafetyFactorHack = 0.9375f;
        private const float k_mouseWheelToHeightRatio = -0.0004f;

        public override string GetName()
        {
            return "Stamp Terrain (Extended)";
        }

        public override string GetDesc()
        {
            return "Stamp on the terrain with additional mouse controls.\n\nLeft click: Stamp brush on the terrain.\nCtrl + Mouse drag left/right: Resize brush.\nCtrl + Mousewheel: Rotate brush.\nCtrl + Shift + Mousewheel: Adjust stamp height.";
        }

        public override void OnSceneGUI(Terrain terrain, IOnSceneGUI editContext)
        {
            Event evt = Event.current;

            // brush rotation
            if (evt.control && !evt.shift && evt.type == EventType.ScrollWheel)
            {
                m_BrushRotation += Event.current.delta.y;

                if (m_BrushRotation >= 360)
                {
                    m_BrushRotation -= 360;
                }

                if (m_BrushRotation < 0)
                {
                    m_BrushRotation += 360;
                }

                m_BrushRotation %= 360;

                evt.Use();
                editContext.Repaint();
            }

            // brush resize
            if (evt.control && evt.type == EventType.MouseDrag)
            {

                m_BrushSize += Event.current.delta.x;

                evt.Use();
                editContext.Repaint();
            }

            // stamp height
            if (evt.control && evt.shift && evt.type == EventType.ScrollWheel)
            {
                m_StampHeight += Event.current.delta.y * k_mouseWheelToHeightRatio * editContext.raycastHit.distance;

                evt.Use();
                editContext.Repaint();
            }

            if (evt.type != EventType.Repaint)
                return;

            if (editContext.hitValidTerrain)
            {
                BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, editContext.raycastHit.textureCoord, m_BrushSize,m_BrushRotation);
                PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds(), 1);

                Material material = TerrainPaintUtilityEditor.GetDefaultBrushPreviewMaterial();

                TerrainPaintUtilityEditor.DrawBrushPreview( paintContext, TerrainPaintUtilityEditor.BrushPreview.SourceRenderTexture, editContext.brushTexture, brushXform, material, 0);

                ApplyBrushInternal(paintContext, m_BrushStrength, editContext.brushTexture, brushXform, terrain);

                RenderTexture.active = paintContext.oldRenderTexture;

                material.SetTexture("_HeightmapOrig", paintContext.sourceRenderTexture);

                TerrainPaintUtilityEditor.DrawBrushPreview( paintContext, TerrainPaintUtilityEditor.BrushPreview.DestinationRenderTexture, editContext.brushTexture, brushXform, material, 1);

                TerrainPaintUtility.ReleaseContextResources(paintContext);
            }
        }

        public override void OnInspectorGUI(Terrain terrain, IOnInspectorGUI editContext)
        {
            EditorGUI.BeginChangeCheck();
            {
                EditorGUI.BeginChangeCheck();

                // stamp height and inverted stamp height: keep height values positive for the user
                float stampHeight = Mathf.Abs(m_StampHeight);
                bool invertStampHeight = m_StampHeight < 0.0f;

                stampHeight = EditorGUILayout.Slider(Styles.stampHeight, stampHeight, 0, terrain.terrainData.size.y);
                invertStampHeight = EditorGUILayout.Toggle(Styles.invertStampHeight, invertStampHeight);

                if (EditorGUI.EndChangeCheck())
                {
                    m_StampHeight = (invertStampHeight ? -stampHeight : stampHeight);
                }
            }

            // show in-built brush selection
            editContext.ShowBrushesGUI(5, BrushGUIEditFlags.Select);

            // custom controls for brush
            m_BrushSize = EditorGUILayout.Slider(Styles.brushSize, m_BrushSize, 0.1f, Mathf.Round(Mathf.Min(terrain.terrainData.size.x, terrain.terrainData.size.z) * brushSizeSafetyFactorHack));
            m_BrushStrength = AddPercentSlider(Styles.brushOpacity, m_BrushStrength, 0, 1);
            m_BrushRotation = EditorGUILayout.Slider(Styles.brushRotation, m_BrushRotation, 0, 359);

            if (EditorGUI.EndChangeCheck())
            {
                Save(true);
            }

            base.OnInspectorGUI(terrain, editContext);
        }


        public override bool OnPaint(Terrain terrain, IOnPaint editContext)
        {
            if (Event.current.type == EventType.MouseDrag)
                return true;

            BrushTransform brushXform = TerrainPaintUtility.CalculateBrushTransform(terrain, editContext.uv, m_BrushSize, m_BrushRotation);
            PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(terrain, brushXform.GetBrushXYBounds());

            ApplyBrushInternal(paintContext, m_BrushStrength, editContext.brushTexture, brushXform, terrain);

            TerrainPaintUtility.EndPaintHeightmap(paintContext, "Terrain Tools - Stamp Tool Extended");

            return true;
        }

        private void ApplyBrushInternal(PaintContext paintContext, float brushStrength, Texture brushTexture, BrushTransform brushXform, Terrain terrain)
        {
            Material material = TerrainPaintUtility.GetBuiltinPaintMaterial();

            float height = m_StampHeight / terrain.terrainData.size.y;

            Vector4 brushParams = new Vector4(brushStrength, 0.0f, height, 1.0f);

            material.SetTexture("_BrushTex", brushTexture);
            material.SetVector("_BrushParams", brushParams);

            TerrainPaintUtility.SetupTerrainToolMaterialProperties(paintContext, brushXform, material);

            Graphics.Blit(paintContext.sourceRenderTexture, paintContext.destinationRenderTexture, material, (int)TerrainPaintUtility.BuiltinPaintMaterialPasses.StampHeight);
        }

        private float AddPercentSlider(GUIContent guiContent, float valueInPercent, float minValue, float maxValue)
        {
            EditorGUI.BeginChangeCheck();

            float value = EditorGUILayout.Slider(guiContent, Mathf.Round(valueInPercent * 100f), minValue * 100f, maxValue * 100f);

            if (EditorGUI.EndChangeCheck())
            {
                return value / 100f;
            }

            return valueInPercent;
        }

    }
}
