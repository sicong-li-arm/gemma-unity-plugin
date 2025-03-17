# Gemma.cpp for Unity

This plugin provides C# bindings to the C API developed for Gemma.cpp.

## Dependencies
This plugin depends upon the following Unity Package Manager (UPM) packages.

- **UniTask** >= 2.5.10 ([link](https://github.com/Cysharp/UniTask.git))\
  This is to allow for usage of the Unity API from within onTokenReceived callbacks, to modify UI etc as the Unity API is not threadsafe.

Note that if you add this package to your project using UPM, Unity should automatically fetch UniTask for you.

## Usage
Add this repository's URL to the Unity Package Manager in your Unity project.
