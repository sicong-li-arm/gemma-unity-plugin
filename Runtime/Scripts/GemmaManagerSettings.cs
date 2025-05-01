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
using System.IO;

namespace GemmaCpp
{
    public enum GemmaWeightFormat
    {
        f32,    // 32-bit floating point
        bf16,   // Brain floating point (16-bit)
        sfp     // Structured floating point
    }

    [CreateAssetMenu(fileName = "GemmaSettings", menuName = "Gemma/Settings")]
    public class GemmaManagerSettings : ScriptableObject
    {
        [Header("Model Configuration")]
        [SerializeField] private GemmaModelType modelType = GemmaModelType.Gemma2B;
        [SerializeField] private GemmaWeightFormat weightFormat = GemmaWeightFormat.sfp;

        [Header("Model Files")]
        [SerializeField, Tooltip("Folder name in StreamingAssets containing the model files")]
        private string modelFolder = "gemma-3.0-4b";

        [SerializeField, Tooltip("Name of the tokenizer file (e.g., tokenizer.model)")]
        private string tokenizerFileName = "tokenizer.spm";

        [SerializeField, Tooltip("Name of the weights file")]
        private string weightsFileName = "4b-it-sfp.sbs";
        [SerializeField, Tooltip("Model type string")]
        private string modelFlag = "gemma3-4b";

        [Header("Generation Settings")]
        [SerializeField, Tooltip("Maximum number of tokens to generate per turn")]
        private int maxGeneratedTokens = 384;

        [SerializeField, Range(0f, 1f), Tooltip("Temperature for text generation (higher = more random)")]
        private float temperature = 0.7f;

        [SerializeField, Range(0f, 1f), Tooltip("Top-p sampling (nucleus sampling) threshold")]
        private float topP = 0.9f;

        // Properties
        public string ModelFlag => modelFlag;
        public GemmaWeightFormat WeightFormat => weightFormat;
        public string ModelPath => Path.Combine(Application.streamingAssetsPath, modelFolder);
        public string TokenizerPath => Path.Combine(ModelPath, tokenizerFileName);
        public string WeightsPath => Path.Combine(ModelPath, weightsFileName);
        public int MaxGeneratedTokens => maxGeneratedTokens;
        public float Temperature => temperature;
        public float TopP => topP;

        private void OnValidate()
        {
            // Validate paths
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(ModelPath))
            {
                if (!Directory.Exists(ModelPath))
                {
                    Debug.LogWarning($"Model folder not found: {ModelPath}");
                }
                else
                {
                    if (!File.Exists(TokenizerPath))
                    {
                        Debug.LogWarning($"Tokenizer file not found: {TokenizerPath}");
                    }
                    if (!File.Exists(WeightsPath))
                    {
                        Debug.LogWarning($"Weights file not found: {WeightsPath}");
                    }
                }
            }
#endif

            maxGeneratedTokens = Mathf.Max(1, maxGeneratedTokens);
            temperature = Mathf.Clamp(temperature, 0f, 1f);
            topP = Mathf.Clamp(topP, 0f, 1f);
        }
    }
}