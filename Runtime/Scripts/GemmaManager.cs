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
    }
}