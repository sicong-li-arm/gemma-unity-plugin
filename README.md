# Gemma.cpp for Unity

This plugin provides C# bindings and a high-level Unity integration for the [Gemma.cpp](https://github.com/google/gemma.cpp) library, allowing you to run Google's Gemma models directly within your Unity projects.

This is not an officially supported Google product.

## Dependencies

This plugin depends upon the following Unity Package Manager (UPM) package:

-   **UniTask** >= 2.5.10 ([link](https://github.com/Cysharp/UniTask.git))\
    This is used by `GemmaManager` to handle asynchronous operations and ensure callbacks (like `onTokenReceived`) can safely interact with the Unity API from background threads.

Note that if you add this package to your project using UPM, Unity should automatically fetch UniTask for you.

## Setup & Usage

1.  **Add Package:** Add this repository's URL to the Unity Package Manager in your Unity project.
2.  **Create Settings:** Create a `GemmaManagerSettings` ScriptableObject asset (Assets -> Create -> GemmaCpp -> Gemma Manager Settings). Configure it with the paths to your downloaded Gemma model weights, tokenizer file, and desired model parameters (Model Type, Weight Format, Temperature).
3.  **Add Component:** Add the `GemmaManager` component to a GameObject in your scene.
4.  **Assign Settings:** Drag your created `GemmaManagerSettings` asset onto the `Settings` field of the `GemmaManager` component in the Inspector.
5.  **Use in Script:** Get a reference to the `GemmaManager` component in your scripts and call its methods (see API Reference below).

## Recommended Usage Patterns

### Managing Multiple NPC Conversations

**Note:** This pattern relies on multiturn conversation history. `GemmaManager` automatically enables multiturn mode during initialization.

A recommended pattern for managing conversations with multiple Non-Player Characters (NPCs) is to use Gemma's conversation context feature:

1.  **Create Context per NPC:** In order to interact with an NPC, create a unique conversation context for them using `GemmaManager.CreateConversation("NPC_UniqueID")`, where `"NPC_UniqueID"` is a distinct identifier for that character (e.g., their name or instance ID).
2.  **Switch Context:** Before generating a response for a specific NPC, switch to their context using `GemmaManager.SwitchConversation("NPC_UniqueID")`.
3.  **Pre-warm with System-like Prompt:** *Before* the player's first interaction with the NPC, send an initial `GenerateResponseAsync` call. The prompt for this call should contain the NPC's persona, role, and any relevant background information (e.g., `"You are Elara, a wise old elf living in the Whispering Woods. You are knowledgeable about ancient runes but wary of strangers."`). This initial call pre-warms the model's KV cache with the NPC's context. You might discard the response from this initial call or use a minimal prompt that elicits a short, neutral response.
4.  **Generate Conversational Responses:** For subsequent interactions, call `GenerateResponseAsync` with *only the current player input* as the prompt (e.g., `"Can you tell me about the Rune of Binding?"`). You do not need to manually append the history; the model automatically uses the history stored within the active conversation context.
5.  **Reset/Delete Contexts:** Use `ResetCurrentConversation()` (after switching to the NPC's context) or `DeleteConversation("NPC_UniqueID")` when appropriate (e.g., end of session, NPC despawns).

This approach keeps each NPC's conversational memory separate and allows you to tailor their responses using targeted prompts within their dedicated context.

## API Reference - `GemmaManager`

The `GemmaManager` MonoBehaviour provides a high-level interface for interacting with the Gemma model within Unity.

**Configuration (Inspector)**

*   **Settings:** (Required) Assign a `GemmaManagerSettings` ScriptableObject containing model paths and parameters.
*   **Verbose Logging:** Enable detailed logging from both the C# wrapper and the underlying native library.

**Methods**

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

## `GemmaManagerSettings`

This ScriptableObject holds the configuration for the Gemma model used by `GemmaManager`.

*   **Tokenizer Path:** Filesystem path to the Gemma tokenizer file (`tokenizer.spm`).
*   **Weights Path:** Filesystem path to the Gemma model weights file (e.g., `.sbs` file).
*   **Model Flag:** The model type string (e.g., "2b-it", "7b-it").
*   **Weight Format:** The format of the weights file (e.g., `Sfp`, `Bf16`).
*   **Temperature:** Sampling temperature for generation (e.g., 0.9).

---

## Low-Level API - `GemmaInterop` (`GemmaCpp.Gemma`)

This class provides direct C# bindings to the native `gemma.dll` functions. It's used internally by `GemmaManager` but can be used directly for more advanced scenarios.

**Note:** When using `GemmaInterop` directly, you are responsible for managing threading and ensuring Unity API calls are made from the main thread if using callbacks.

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
