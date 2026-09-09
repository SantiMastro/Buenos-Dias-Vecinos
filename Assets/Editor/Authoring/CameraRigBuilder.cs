using BuenosDias.Presentation;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BuenosDias.EditorTools.Authoring
{
    /// <summary>
    /// Arma la cámara pixel-perfect y la luz global de la escena.
    /// </summary>
    public static class CameraRigBuilder
    {
        /// <summary>
        /// Cielo de mediodía. Sale de la paleta del proyecto (#A8B4C6): el
        /// skyline es una silueta plana, así que el cielo ES el color de fondo
        /// de la cámara. A partir de la fase 7 lo maneja DayCycleConfig.
        /// </summary>
        private static readonly Color MiddaySky = new Color32(0xA8, 0xB4, 0xC6, 0xFF);

        /// <summary>Crea la cámara y la deja siguiendo al objetivo indicado.</summary>
        public static Camera BuildCamera(Transform followTarget)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, SceneLayout.CameraCenterY, SceneLayout.CameraZ);

            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = SceneLayout.OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = MiddaySky;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            ConfigurePixelPerfect(go);

            var follow = go.AddComponent<PixelPerfectFollowCamera>();
            SerializedFieldUtility.SetReference(follow, "target", followTarget);

            return camera;
        }

        /// <summary>Crea la luz global que después maneja el ciclo de día.</summary>
        public static Light2D BuildGlobalLight()
        {
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = Color.white;
            light.intensity = 1f;
            return light;
        }

        private static void ConfigurePixelPerfect(GameObject cameraObject)
        {
            var ppc = cameraObject.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = (int)SceneLayout.PixelsPerUnit;
            ppc.refResolutionX = SceneLayout.ReferenceWidth;
            ppc.refResolutionY = SceneLayout.ReferenceHeight;

            // La spec pedía "Upscale Render Texture y Pixel Snapping" a la vez,
            // pero en URP 17 son dos estados EXCLUYENTES de un mismo enum: las
            // propiedades viejas upscaleRT y pixelSnapping están [Obsolete] y
            // cada setter pisa al otro. UpscaleRenderTexture es el que conviene:
            // renderiza a una RT del tamaño de referencia, con lo cual el
            // movimiento subpíxel ya es imposible y subsume a Pixel Snapping.
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;

            // El default de Unity 6 es RetroAA, que mete un filtrado bilinear en
            // el escalado final y ablanda el pixel art. El campo es privado.
            SerializedFieldUtility.SetEnum(
                ppc, "m_FilterMode", (int)PixelPerfectCamera.PixelPerfectFilterMode.Point);
        }
    }
}
