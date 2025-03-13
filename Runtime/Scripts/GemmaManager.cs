using UnityEngine;
using System;
using Cysharp.Threading.Tasks;

namespace GemmaCpp
{
    public class GemmaManager : MonoBehaviour
    {
        [SerializeField]
        public GemmaManagerSettings settings;

        private Gemma gemma;
        private bool isInitialized;

        private void Start()
        {
            InitializeGemma();
        }

        private void OnDestroy()
        {
            if (gemma != null)
            {
                gemma.Dispose();
                gemma = null;
                isInitialized = false;
                Debug.Log("Gemma resources cleaned up");
            }
        }

        private void InitializeGemma()
        {
            try
            {
                var modelFlag = GemmaModelUtils.GetFlagsForModelType(settings.ModelType)[0];
                gemma = new Gemma(
                    settings.TokenizerPath,
                    modelFlag,
                    settings.WeightsPath,
                    settings.WeightFormat.ToString(),
                    8192 // change for gemma3
                );
                isInitialized = true;
                Debug.Log("Gemma initialized successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error initializing Gemma: {e.Message}");
            }
        }

        public async UniTask<string> GenerateResponseAsync(string prompt, Gemma.TokenCallback onTokenReceived = null)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Gemma is not initialized");
            }

            // Create a callback that uses UniTask's PlayerLoopTiming to run on main thread
            Gemma.TokenCallback wrappedCallback = null;
            if (onTokenReceived != null)
            {
                wrappedCallback = (token) =>
                {
                    bool result = false;
                    UniTask.Post(() =>
                    {
                        result = onTokenReceived(token);
                    }, PlayerLoopTiming.Update);
                    return true;
                };
            }

            // Run the generation on a background thread
            return await UniTask.RunOnThreadPool(() =>
            {
                try
                {
                    return gemma.Generate(prompt, wrappedCallback);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to generate response: {e.Message}");
                }
            });
        }

        /// <summary>
        /// Generate a response from Gemma using both text and image input
        /// </summary>
        /// <param name="prompt">The text prompt to send to Gemma</param>
        /// <param name="renderTexture">The RenderTexture containing the image to process</param>
        /// <param name="onTokenReceived">Optional callback to receive tokens as they're generated</param>
        /// <returns>The generated response</returns>
        public async UniTask<string> GenerateMultimodalResponseAsync(string prompt, RenderTexture renderTexture, Gemma.TokenCallback onTokenReceived = null)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("Gemma is not initialized");
            }

            if (renderTexture == null)
            {
                throw new ArgumentNullException(nameof(renderTexture), "RenderTexture cannot be null");
            }

            // Create a callback that uses UniTask's PlayerLoopTiming to run on main thread
            Gemma.TokenCallback wrappedCallback = null;
            if (onTokenReceived != null)
            {
                wrappedCallback = (token) =>
                {
                    bool result = false;
                    UniTask.Post(() =>
                    {
                        result = onTokenReceived(token);
                    }, PlayerLoopTiming.Update);
                    return true;
                };
            }

            // Convert RenderTexture to float array on the main thread
            float[] imageData = await ConvertRenderTextureToFloatArrayAsync(renderTexture);

            // Run the generation on a background thread
            return await UniTask.RunOnThreadPool(() =>
            {
                try
                {
                    return gemma.GenerateMultimodal(prompt, imageData, renderTexture.width, renderTexture.height, wrappedCallback);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to generate multimodal response: {e.Message}");
                }
            });
        }

        /// <summary>
        /// Converts a RenderTexture to a float array of RGB values in the range [0,1]
        /// </summary>
        /// <param name="renderTexture">The RenderTexture to convert</param>
        /// <returns>Float array of RGB values</returns>
        private async UniTask<float[]> ConvertRenderTextureToFloatArrayAsync(RenderTexture renderTexture)
        {
            // This needs to run on the main thread because it accesses Unity objects
            await UniTask.SwitchToMainThread();

            int width = renderTexture.width;
            int height = renderTexture.height;

            // Create a temporary texture to read the pixels
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            // Remember the current active render texture
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                // Set the render texture as active and read the pixels
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                // Get the raw pixel data
                Color32[] pixels = texture.GetPixels32();

                // Convert to float array in the format expected by Gemma (RGB values in [0,1])
                float[] imageData = new float[pixels.Length * 3];
                for (int i = 0; i < pixels.Length; i++)
                {
                    imageData[i * 3 + 0] = pixels[i].r / 255.0f; // R
                    imageData[i * 3 + 1] = pixels[i].g / 255.0f; // G
                    imageData[i * 3 + 2] = pixels[i].b / 255.0f; // B
                }

                return imageData;
            }
            finally
            {
                // Restore the previous active render texture
                RenderTexture.active = previousActive;

                // Clean up the temporary texture
                Destroy(texture);
            }
        }
    }
}