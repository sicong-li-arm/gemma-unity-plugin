// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System;
using System.Collections.Generic; // Added for Dictionary
using System.Text;             // Added for StringBuilder
using Cysharp.Threading.Tasks;

namespace GemmaCpp
{
    public class GemmaManager : MonoBehaviour
    {
        [SerializeField]
        public GemmaManagerSettings settings;

        [SerializeField]
        private bool verboseLogging = false;

        private Gemma gemma;
        private bool isInitialized;

        public bool Initialized => isInitialized;

        private void Start()
        {
            Debug.Log("GemmaManager: Starting initialization");
            InitializeGemma();
        }

        private void OnDestroy()
        {
            if (gemma != null)
            {
                gemma.Dispose();
                gemma = null;
                isInitialized = false;
                Debug.Log("GemmaManager: Resources cleaned up");
            }
        }

        private void InitializeGemma()
        {
            try
            {
                Debug.Log($"GemmaManager: Initializing with tokenizer: {settings.TokenizerPath}, weights: {settings.WeightsPath}");
                Debug.Log($"GemmaManager: Using max tokens: {settings.MaxGeneratedTokens}");

                gemma = new Gemma(
                    settings.TokenizerPath,
                    settings.WeightsPath,
                    settings.MaxGeneratedTokens
                );
                gemma.EnableLogging(verboseLogging);
                isInitialized = true;

                verboseLogging = true;

                // Apply settings and logging after successful initialization
                if (isInitialized)
                {
                    try
                    {
                        // Enable Multiturn mode by default for Manager usage
                        gemma.SetMultiturn(true);
                        Debug.Log("GemmaManager: Multiturn mode enabled by default.");

                        gemma.SetTemperature(settings.Temperature);

                        if (verboseLogging)
                        {
                            Debug.Log($"GemmaManager: Applied settings - Multiturn: Enabled, Temperature: {settings.Temperature}, Native Logging: Enabled");
                        }
                        else
                        {
                            Debug.Log($"GemmaManager: Applied settings - Multiturn: Enabled, Temperature: {settings.Temperature}");
                        }
                    }
                    catch (Exception settingsEx)
                    {
                        Debug.LogWarning($"GemmaManager: Failed to apply some settings - {settingsEx.Message}");
                        // Continue initialization even if settings fail to apply
                    }
                }

                Debug.Log("GemmaManager: Initialized successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Error initializing - {e.Message}\n{e.StackTrace}");
            }
        }
        public enum PrewarmState
        {
            NotApplicable,
            Pending,
            InProgress,
            Done
        }
        public delegate void PrewarmStatusCallback(string conversation, PrewarmState state);

        public async UniTask Prewarm(Dictionary<string, string> conversations, PrewarmStatusCallback callback = null)
        {

            // Using Time.time for elapsed game time, formatted to 3 decimal places (F3)
            string timestamp = $"[{Time.time:F3}]";
            Debug.Log($"GemmaManager::Prewarm(): {timestamp} Prewarm sequence started. Waiting for GemmaManager initialization...");

            while (!isInitialized)
            {
                timestamp = $"[{Time.time:F3}]";
                Debug.Log($"GemmaManager::Prewarm(): {timestamp} Waiting for GemmaManager to initialize...");
                await UniTask.Delay(TimeSpan.FromSeconds(1)); // Wait 1 second
            }

            timestamp = $"[{Time.time:F3}]";
            Debug.Log($"GemmaManager::Prewarm(): {timestamp} GemmaManager initialized. Starting conversation prewarming.");

            int count = 0;
            foreach (var kvp in conversations)
            {
                string conversationName = kvp.Key;
                string initialPrompt = kvp.Value; // Use the value from the dictionary as the prompt

                timestamp = $"[{Time.time:F3}]";
                if (string.IsNullOrEmpty(conversationName))
                {
                    Debug.LogWarning($"GemmaManager::Prewarm(): {timestamp} Skipping conversation at index {count} due to missing conversation name.");
                    count++;
                    continue;
                }

                // Use 'this' to access GemmaManager methods
                if (!this.HasConversation(conversationName))
                {
                    Debug.Log($"GemmaManager::Prewarm(): {timestamp} Prewarming conversation: {conversationName}");
                    try
                    {
                        this.CreateConversation(conversationName);
                        Debug.Log($"GemmaManager::Prewarm(): {timestamp} Successfully created conversation: {conversationName}");

                        // Switch to the new conversation and generate initial response
                        this.SwitchConversation(conversationName);
                        if (!string.IsNullOrEmpty(initialPrompt))
                        {
                            Debug.Log($"GemmaManager::Prewarm(): {timestamp} Generating initial response for {conversationName}...");
                            if (callback != null)
                            {
                                callback(conversationName, PrewarmState.InProgress);
                            }
                            await UniTask.RunOnThreadPool(() => {
                                gemma.Generate(initialPrompt, 64);
                                gemma.SaveConversation();
                                //GenerateResponseAsync(initialPrompt);
                            });
                            if (callback != null)
                            {
                                callback(conversationName, PrewarmState.Done);
                            }
                            Debug.Log($"GemmaManager::Prewarm(): {timestamp} Initial response generated for {conversationName}.");
                        }
                        else
                        {
                             Debug.Log($"GemmaManager::Prewarm(): {timestamp} No initial prompt provided for {conversationName}, skipping generation.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"GemmaManager::Prewarm(): {timestamp} Failed to create or prewarm conversation {conversationName}: {ex.Message}");
                    }
                }
                else
                {
                    Debug.Log($"GemmaManager::Prewarm(): {timestamp} Conversation already exists: {conversationName}");
                }
                count++;
            }

            timestamp = $"[{Time.time:F3}]";
            Debug.Log($"GemmaManager::Prewarm(): {timestamp} Prewarm finished processing {count} conversations.");

            SwitchConversation("default");
        }

        public async UniTask<string> GenerateResponseAsync(string prompt, Gemma.TokenCallback onTokenReceived = null)
        {
            if (!isInitialized)
            {
                Debug.LogError("GemmaManager: Cannot generate response - not initialized");
                throw new InvalidOperationException("Gemma is not initialized");
            }

            if (verboseLogging)
            {
                Debug.Log($"GemmaManager: Generating response for prompt: \"{TruncateForLogging(prompt)}\"");
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
                        /*
                        if (verboseLogging)
                        {
                            Debug.Log($"GemmaManager: Token received: \"{TruncateForLogging(token)}\"");
                        }
                        */
                    }, PlayerLoopTiming.Update);
                    return true;
                };
            }

            // Run the generation on a background thread
            Debug.Log("GemmaManager: Starting text generation on background thread");
            try
            {
                string result = await UniTask.RunOnThreadPool(() =>
                {
                    try
                    {
                        Debug.Log(prompt);
                        return gemma.Generate(prompt, wrappedCallback, settings.MaxGeneratedTokens);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"GemmaManager: Generation failed - {e.Message}\n{e.StackTrace}");
                        throw new Exception($"Failed to generate response: {e.Message}", e);
                    }
                });

                if (verboseLogging)
                {
                    Debug.Log($"GemmaManager: Generation complete, result length: {result.Length} chars");
                }
                else
                {
                    Debug.Log("GemmaManager: Text generation complete");
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Exception during generation - {e.Message}\n{e.StackTrace}");
                throw;
            }
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
                Debug.LogError("GemmaManager: Cannot generate multimodal response - not initialized");
                throw new InvalidOperationException("Gemma is not initialized");
            }

            if (renderTexture == null)
            {
                Debug.LogError("GemmaManager: RenderTexture is null");
                throw new ArgumentNullException(nameof(renderTexture), "RenderTexture cannot be null");
            }

            Debug.Log($"GemmaManager: Starting multimodal generation with image {renderTexture.width}x{renderTexture.height}");
            if (verboseLogging)
            {
                Debug.Log($"GemmaManager: Multimodal prompt: \"{TruncateForLogging(prompt)}\"");
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
                        if (verboseLogging)
                        {
                            Debug.Log($"GemmaManager: Multimodal token received: \"{TruncateForLogging(token)}\"");
                        }
                    }, PlayerLoopTiming.Update);
                    return true;
                };
            }

            try
            {
                // Convert RenderTexture to float array on the main thread
                Debug.Log("GemmaManager: Converting RenderTexture to float array");
                float[] imageData = await ConvertRenderTextureToFloatArrayAsync(renderTexture);
                Debug.Log($"GemmaManager: Converted image to float array, size: {imageData.Length} elements");

                // Run the generation on a background thread
                Debug.Log("GemmaManager: Starting multimodal generation on background thread");
                string result = await UniTask.RunOnThreadPool(() =>
                {
                    try
                    {
                        return gemma.GenerateMultimodal(prompt, imageData, renderTexture.width, renderTexture.height, wrappedCallback, settings.MaxGeneratedTokens);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"GemmaManager: Multimodal generation failed - {e.Message}\n{e.StackTrace}");
                        throw new Exception($"Failed to generate multimodal response: {e.Message}", e);
                    }
                });

                if (verboseLogging)
                {
                    Debug.Log($"GemmaManager: Multimodal generation complete, result length: {result.Length} chars");
                }
                else
                {
                    Debug.Log("GemmaManager: Multimodal generation complete");
                }

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Exception during multimodal generation - {e.Message}\n{e.StackTrace}");
                throw;
            }
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
            Debug.Log("GemmaManager: Starting RenderTexture conversion on main thread");

            int width = renderTexture.width;
            int height = renderTexture.height;
            Debug.Log($"GemmaManager: Processing image of size {width}x{height}");

            // Create a temporary texture to read the pixels
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            Debug.Log("GemmaManager: Created temporary Texture2D");

            // Remember the current active render texture
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                // Set the render texture as active and read the pixels
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                Debug.Log("GemmaManager: Read pixels from RenderTexture");

                // Get the raw pixel data
                Color32[] pixels = texture.GetPixels32();
                Debug.Log($"GemmaManager: Got {pixels.Length} pixels from texture");

                // Convert to float array in the format expected by Gemma (RGB values in [0,1])
                float[] imageData = new float[pixels.Length * 3];
                for (int i = 0; i < pixels.Length; i++)
                {
                    imageData[i * 3 + 0] = pixels[i].r / 255.0f; // R
                    imageData[i * 3 + 1] = pixels[i].g / 255.0f; // G
                    imageData[i * 3 + 2] = pixels[i].b / 255.0f; // B
                }

                if (verboseLogging)
                {
                    // Log some sample pixel values to verify conversion
                    Debug.Log($"GemmaManager: Sample pixel values (first 3 pixels):");
                    for (int i = 0; i < Math.Min(3, pixels.Length); i++)
                    {
                        Debug.Log($"  Pixel {i}: R={imageData[i * 3 + 0]:F2}, G={imageData[i * 3 + 1]:F2}, B={imageData[i * 3 + 2]:F2}");
                    }
                }

                Debug.Log($"GemmaManager: Converted to float array with {imageData.Length} elements");
                return imageData;
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Error converting RenderTexture - {e.Message}\n{e.StackTrace}");
                throw;
            }
            finally
            {
                // Restore the previous active render texture
                RenderTexture.active = previousActive;
                Debug.Log("GemmaManager: Restored previous RenderTexture.active");

                // Clean up the temporary texture
                Destroy(texture);
                Debug.Log("GemmaManager: Destroyed temporary texture");
            }
        }

        #region Conversation Management

        /// <summary>
        /// Enables / disables multiturn.
        /// </summary>
        public void SetMultiturn(bool enable)
        {
            gemma.SetMultiturn(enable);
        }

        /// <summary>
        /// Resets the current conversation context in the Gemma model.
        /// </summary>
        public void ResetCurrentConversation()
        {
            if (!isInitialized)
            {
                Debug.LogError("GemmaManager: Cannot reset conversation - not initialized");
                throw new InvalidOperationException("Gemma is not initialized");
            }

            try
            {
                gemma.ResetConversation(); // Call the method from GemmaInterop
                Debug.Log("GemmaManager: Current conversation reset requested.");
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Error resetting conversation - {e.Message}\n{e.StackTrace}");
                throw; // Re-throw or handle as appropriate
            }
        }

        /// <summary>
        /// Creates a new named conversation context.
        /// </summary>
        /// <param name="conversationName">The unique name for the new conversation.</param>
        /// <returns>True if the conversation was created successfully, false otherwise.</returns>
        public bool CreateConversation(string conversationName)
        {
            if (!isInitialized)
            {
                Debug.LogError("GemmaManager: Cannot create conversation - not initialized");
                throw new InvalidOperationException("Gemma is not initialized");
            }
            if (string.IsNullOrEmpty(conversationName))
            {
                Debug.LogError("GemmaManager: Conversation name cannot be null or empty.");
                return false;
            }

            try
            {
                bool result = gemma.CreateConversation(conversationName);
                if (verboseLogging) Debug.Log($"GemmaManager: Create conversation '{conversationName}' result: {result}");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Error creating conversation '{conversationName}' - {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Switches the active conversation context to the specified name.
        /// </summary>
        /// <param name="conversationName">The name of the conversation to switch to.</param>
        /// <returns>True if the switch was successful, false otherwise (e.g., conversation doesn't exist).</returns>
        public bool SwitchConversation(string conversationName)
        {
            if (!isInitialized)
            {
                Debug.LogError("GemmaManager: Cannot switch conversation - not initialized");
                throw new InvalidOperationException("Gemma is not initialized");
            }
            if (string.IsNullOrEmpty(conversationName))
            {
                Debug.LogError("GemmaManager: Conversation name cannot be null or empty.");
                return false;
            }

            try
            {
                bool result = gemma.SwitchConversation(conversationName);
                 if (verboseLogging) Debug.Log($"GemmaManager: Switch to conversation '{conversationName}' result: {result}");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Error switching to conversation '{conversationName}' - {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a named conversation context.
        /// </summary>
        /// <param name="conversationName">The name of the conversation to delete.</param>
        /// <returns>True if deletion was successful, false otherwise.</returns>
        public bool DeleteConversation(string conversationName)
        {
            if (!isInitialized)
            {
                Debug.LogError("GemmaManager: Cannot delete conversation - not initialized");
                throw new InvalidOperationException("Gemma is not initialized");
            }
             if (string.IsNullOrEmpty(conversationName))
            {
                Debug.LogError("GemmaManager: Conversation name cannot be null or empty.");
                return false;
            }

            try
            {
                bool result = gemma.DeleteConversation(conversationName);
                 if (verboseLogging) Debug.Log($"GemmaManager: Delete conversation '{conversationName}' result: {result}");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Error deleting conversation '{conversationName}' - {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Checks if a conversation with the specified name exists.
        /// </summary>
        /// <param name="conversationName">The name of the conversation to check.</param>
        /// <returns>True if the conversation exists, false otherwise.</returns>
        public bool HasConversation(string conversationName)
        {
            if (!isInitialized)
            {
                Debug.LogError("GemmaManager: Cannot check conversation - not initialized");
                throw new InvalidOperationException("Gemma is not initialized");
            }
             if (string.IsNullOrEmpty(conversationName))
            {
                Debug.LogError("GemmaManager: Conversation name cannot be null or empty.");
                return false;
            }

            try
            {
                bool result = gemma.HasConversation(conversationName);
                 if (verboseLogging) Debug.Log($"GemmaManager: Has conversation '{conversationName}' result: {result}");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Error checking for conversation '{conversationName}' - {e.Message}\n{e.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Gets the name of the currently active conversation.
        /// </summary>
        /// <returns>The name of the active conversation, or null/empty if none is active or an error occurs.</returns>
        public string GetCurrentConversation()
        {
            if (!isInitialized)
            {
                Debug.LogError("GemmaManager: Cannot get current conversation - not initialized");
                throw new InvalidOperationException("Gemma is not initialized");
            }

            try
            {
                string conversationName = gemma.GetCurrentConversation(); // Assuming GemmaInterop.Gemma has this method
                if (verboseLogging) Debug.Log($"GemmaManager: Current conversation is '{conversationName}'");
                return conversationName;
            }
            catch (Exception e)
            {
                Debug.LogError($"GemmaManager: Error getting current conversation - {e.Message}\n{e.StackTrace}");
                throw; // Re-throw or handle as appropriate
            }
        }

        #endregion

        /// <summary>
        /// Truncates a string for logging purposes to avoid flooding the console
        /// </summary>
        private string TruncateForLogging(string text, int maxLength = 50)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }
    }
}
