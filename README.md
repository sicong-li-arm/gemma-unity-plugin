# Gemma.cpp for Unity

This plugin provides C# bindings to the C API developed for Gemma.cpp.

## Dependencies
This plugin depends upon the following Unity Package Manager (UPM) packages.

- **UniTask** >= 2.5.10 ([link](https://github.com/Cysharp/UniTask.git))\
  This is to allow for usage of the Unity API from within onTokenReceived callbacks, to modify UI etc as the Unity API is not threadsafe.

Note that if you add this package to your project using UPM, Unity should automatically fetch UniTask for you.

## Usage
Add this repository's URL to the Unity Package Manager in your Unity project.

## API Reference

This file provides a C# wrapper around the Gemma C API, allowing C# applications to use the Gemma model.

*   **Contents:**
    *   Defines a `Gemma` class that wraps the `GemmaContext` from the C API.
    *   Uses `DllImport` to import the functions from the `gemma.dll` (or equivalent on other platforms).
    *   Provides C# methods that mirror the C API functions:
        *   `Gemma()`: Constructor to create a `Gemma` instance (which internally creates a `GemmaContext`).
        *   `Dispose()`: Destructor to release resources (and destroy the `GemmaContext`).
        *   `Generate()`: Generates text.
        *   `GenerateMultimodal()`: Generates text with image input.
        *   `CountTokens()`: Counts tokens.
        *   `SetLogCallback()`: (Commented out in the provided code, but the structure is there).
        *   `SetMultiturn()`, `SetTemperature()`, `SetTopK()`, `SetDeterministic()`, `ResetContext()`: Configuration methods.
        *   `CreateContext()`, `SwitchContext()`, `DeleteContext()`, `HasContext()`: Context management methods.
    *   Handles marshalling of data between C# and C (e.g., strings, arrays).
    *   Provides error handling (e.g., throwing a newly defined `GemmaException`).
    *   Uses `GCHandle` to manage the lifetime of callbacks passed to the C API.