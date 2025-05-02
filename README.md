# Gemma.cpp for Unity

This plugin provides C# bindings and a high-level Unity integration for the [Gemma.cpp](https://github.com/google/gemma.cpp) library, allowing you to run Google's Gemma models directly within your Unity projects.

This is not an officially supported Google product.

## Dependencies

This plugin depends upon the following Unity Package Manager (UPM) package:

-   **UniTask** >= 2.5.10 ([link](https://github.com/Cysharp/UniTask.git))\
    This is used by `GemmaManager` to handle asynchronous operations and ensure callbacks (like `onTokenReceived`) can safely interact with the Unity API from background threads.

Note that if this package is added to a Unity project using UPM, Unity should automatically fetch UniTask based on the dependency information.

## Setup & Usage

1.  **Add Package:** Add this repository's URL to the Unity Package Manager in your Unity project.
2.  **Create Settings:** Create a `GemmaManagerSettings` ScriptableObject asset (Assets -> Create -> GemmaCpp -> Gemma Manager Settings). Configure it with the paths to your downloaded Gemma model weights, tokenizer file, and desired model parameters (Model Type, Weight Format, Temperature).
3.  **Add Component:** Add the `GemmaManager` component to a GameObject in your scene.
4.  **Assign Settings:** Drag your created `GemmaManagerSettings` asset onto the `Settings` field of the `GemmaManager` component in the Inspector.
5.  **Use in Script:** Get a reference to the `GemmaManager` component in your scripts and call its methods (see API Reference below).

## Recommended Usage Patterns

### Setting up a `Gemma` instance
A `Gemma` instance is created by passing in the information that would ordinarily be passed to **Gemma.cpp** on the command line.

```csharp
Gemma gemma = new Gemma(
    "tokenizer.spm",    // tokenizer path
    "gemma3-4b",        // model flag
    "4b-it-sfp.sbs",    // weights file path
    "sfp",              // weights format, usually "sfp"
    384                 // maximum number of tokens generated per turn
);
gemma.SetMultiturn(true);
gemma.SetTemperature(1.0f);
gemma.EnableLogging(true);
```

### Managing Multiple NPC Conversations

**Note:** This pattern relies on multiturn conversation history. Ensure that `gemma.SetMultiturn(true)` has been called.

Managing conversations with multiple Non-Player Characters (NPCs) can be achieved by using Gemma.cpp's **conversation context** feature.

In brief, Gemma.cpp keeps track of a number of `ConversationData` structs that each contain a KV cache and an absolute position (i.e. the point in the cache up to which tokens have been generated).

```cpp
// gemma.cpp - gemma/bindings/context.h
struct ConversationData {
  std::unique_ptr<KVCache> kv_cache;
  size_t abs_pos = 0;

  ConversationData(const ModelConfig& model_config, size_t prefill_tbatch_size);
};
```

In this way, each `ConversationData` represents a conversation that the user is having with Gemma, with strict boundaries - what happens in conversation A, does not affect conversation B. By using the various conversation-related methods in Gemma.cpp, it is possible to switch between conversations appropriately, allowing one Gemma model instance to power multiple NPCs.

Each conversation is stored in a key-value store, where the key is a string, and the value is a `ConversationData` struct. The base conversation is `"default"`.

#### Example usage

The user wants to talk to an NPC, "Elara Smith", with the unique string identifier `npc_elara`.

```csharp
// create conversation if it doesn't yet exist
bool conversation_existed = gemma.HasConversation("npc_elara");
if (!conversation_existed) {
    gemma.CreateConversation("npc_elara");
}

// switch to the conversation
gemma.SwitchConversation("npc_elara");

// make sure that multiturn is on
gemma.SetMultiturn(true);

// prewarm the conversation by sending the NPC's biography as a turn
if (!conversation_existed) {
    var elara_bio = "Your name is Elara Smith, a wise old elf living in the Whispering Woods. You are knowledgeable about ancient runes but wary of strangers.";
    gemma.Generate(elara_bio, 256); // we won't display the inference result, so keep maxLength short
}

// ready to talk to the NPC!
```

As Gemma is set up to operate in multiturn mode, when talking to the model the user should only send `the current user input` as the prompt (e.g., `"Can you tell me about the Rune of Binding?"`). It is not necessary to prepend the conversation history; the model automatically uses the history stored within the active conversation context's KV cache.

It may be useful to perform prewarming well in advance of interacting with an NPC. We provide the `Prewarm()` method that takes a `Dictionary<string, string>` object as a parameter for this purpose.

### `GemmaManager`, a Unity `MonoBehaviour` that wraps `Gemma`

`GemmaManager` is a convenience `MonoBehaviour` class that provides a high-level interface for interacting with Gemma within Unity.

After the model has been loaded in `Start()`, use `GeneateResponseAsync()` to perform inference. This method takes a callback as one of its parameters, which uses **UniTask** for thread-safe use of Unity engine APIs (e.g. for updating text boxes and the like).

## API Reference - `GemmaManager`

The `GemmaManager` MonoBehaviour provides a high-level interface for interacting with the Gemma model within Unity.

**Configuration (Inspector)**

*   **Settings:** (Required) Assign a `GemmaManagerSettings` ScriptableObject containing model paths and parameters.
*   **Verbose Logging:** Enable detailed logging from both the C# wrapper and the underlying native library.

**Methods**
*   **`async UniTask Prewarm(Dictionary<string, string> conversations, PrewarmStatusCallback statusCallback = null)`**
    *   Asynchronously prewarms specified conversation contexts before they are actively used.
    *   `conversations`: A dictionary where keys represent the unique names of the conversations to prewarm, and values represent the initial prompt to send to each respective conversation.
    *   `statusCallback`: (Optional) A delegate of type `GemmaManager.PrewarmStatusCallback(string conversationName, PrewarmState state)` that can be provided to receive status updates during the prewarming process. The callback receives the name of the conversation being processed and its subsequent `PrewarmState` (e.g., Pending, InProgress, Done, Skipped, Failed).
    *   For each entry, it ensures the conversation exists (creates if not), switches to it, and generates an initial response using the provided prompt. This helps reduce latency on the first interaction.

*   **`async UniTask<string> GenerateResponseAsync(string prompt, Gemma.TokenCallback onTokenReceived = null)`**
    *   Generates a text response based on the input `prompt`.
    *   Runs the generation on a background thread.
    *   `onTokenReceived`: Optional callback delegate (`bool TokenCallback(string token)`) that receives generated tokens one by one. Callbacks are executed on the main thread via UniTask, allowing safe interaction with Unity APIs (e.g., updating UI elements). Return `true` from the callback to continue generation, `false` to stop early.
    *   Returns the complete generated response string.

*   **`async UniTask<string> GenerateMultimodalResponseAsync(string prompt, RenderTexture renderTexture, Gemma.TokenCallback onTokenReceived = null)`**
    *   Generates a text response based on the input `prompt` and an image provided as a `RenderTexture`.
    *   The `RenderTexture` is automatically converted to the required format on the main thread.
    *   Runs the generation on a background thread.
    *   `onTokenReceived`: Optional callback, same behavior as in `GenerateResponseAsync`. This is the appropriate place to update UI based on incoming tokens.
    *   Returns the complete generated response string.

*   **`void ResetCurrentConversation()`**
    *   Resets the history of the current conversation context in the Gemma model.

*   **`bool CreateConversation(string conversationName)`**
    *   Creates a new, named conversation context. Returns `true` on success.

*   **`bool SwitchConversation(string conversationName)`**
    *   Switches the active context to a previously created conversation. Returns `true` if the conversation exists and was switched to.

*   **`bool DeleteConversation(string conversationName)`**
    *   Deletes a named conversation context. Returns `true` on success.

*   **`bool HasConversation(string conversationName)`**
    *   Checks if a conversation with the given name exists. Returns `true` if it exists.

*   **`string GetCurrentConversation()`**
    *   Gets the name of the currently active conversation context.
    *   Returns the name as a string. Returns null or empty if no conversation is active or an error occurs.

### `GemmaManagerSettings`

This ScriptableObject holds the configuration for the Gemma model used by `GemmaManager`.

*   **Tokenizer Path:** Filesystem path to the Gemma tokenizer file (`tokenizer.spm`).
*   **Weights Path:** Filesystem path to the Gemma model weights file (e.g., `.sbs` file).
*   **Model Flag:** The model type string (e.g., "gemma3-4b").
*   **Weight Format:** The format of the weights file (e.g., `sfp`).
*   **Temperature:** Sampling temperature for generation (e.g., 0.9).

---

## Low-Level API - `GemmaInterop.cs` (`GemmaCpp.Gemma`)

This class provides direct C# bindings to the native `gemma.dll` functions. It is used internally by `GemmaManager` but can be used directly for more advanced scenarios.

**Constructor**

*   **`Gemma(string tokenizerPath, string modelType, string weightsPath, string weightType, int maxLength = 8192)`**
    *   Creates and initializes a native Gemma context. Throws `GemmaException` on failure.

**Methods**

*   **`string Generate(string prompt, int maxLength = 4096)`**
    *   Generates text based on the input `prompt`, to a maximum length of `maxLength`.
*   **`string Generate(string prompt, TokenCallback callback, int maxLength = 4096)`**
    *   Generates text based on the input `prompt`, to a maximum length of `maxLength`. The `TokenCallback` (if provided) is executed directly on the generation thread. Return `true` to continue, `false` to stop.
*   **`string GenerateMultimodal(string prompt, float[] imageData, int imageWidth, int imageHeight, int maxLength = 4096)`**
    *   Generates text based on a text input `prompt`, with an image input as well, to a maximum length of `maxLength`. `imageData` must be a flat array of RGB float values (0.0-1.0).
*   **`string GenerateMultimodal(string prompt, float[] imageData, int imageWidth, int imageHeight, TokenCallback callback, int maxLength = 4096)`**
    *   Generates text with image input. The `TokenCallback` (if provided) is executed directly on the generation thread. Return `true` to continue, `false` to stop.
*   **`int CountTokens(string text)`**
    *   Counts the number of tokens in the given text according to the loaded tokenizer.
*   **`void SetMultiturn(bool enable)`**
    *   Enables or disables multiturn conversation history.
*   **`void SetTemperature(float temperature)`**
    *   Sets the sampling temperature.
*   **`void SetTopK(int topK)`**
    *   Sets the top-K sampling value.
*   **`void SetDeterministic(bool deterministic)`**
    *   Enables or disables deterministic sampling (uses seed 0).
*   **`void ResetConversation()`**
    *   Resets the history of the currently active conversation context.
*   **`bool CreateConversation(string conversationName)`**
    *   Creates a named conversation context.
*   **`bool SwitchConversation(string conversationName)`**
    *   Switches the active context.
*   **`bool DeleteConversation(string conversationName)`**
    *   Deletes a named conversation context.
*   **`bool HasConversation(string conversationName)`**
    *   Checks if a named conversation exists.
*   **`string GetCurrentConversation()`**
    *   Gets the history of the currently active conversation context as a string. Returns `null` on error.
*   **`void EnableLogging(bool enable = true)`**
    *   Enables or disables log messages from the native library via a callback to `Debug.WriteLine`.
*   **`void Dispose()`**
    *   Releases the native Gemma context and associated resources. Must be called when finished. Implements `IDisposable`.

**Exceptions**

*   **`GemmaException`**: Thrown for errors during Gemma operations (e.g., initialization failure, generation failure).
*   **`DllNotFoundException`**: Thrown if the `gemma.dll` (or platform equivalent) cannot be loaded.
*   **`ObjectDisposedException`**: Thrown if methods are called after `Dispose()` has been called.
