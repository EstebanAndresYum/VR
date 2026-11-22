# Dual-dial haptic puzzle: first implementation

## Current behavior

- Each `DialInteraction` reads one tracked XR controller. Hold its grip button and move the controller around the dial pivot to rotate the dial.
- The dial angle is clamped to 0-180 degrees.
- `DualDialPuzzleManager` targets 70 degrees on the left and 120 degrees on the right by default. Both angles must stay within 6 degrees of their targets for 1 second.
- Each hand receives independent proximity pulses. Pulses become more frequent and stronger as its dial approaches the target.
- Optional renderers change color from neutral to red and then teal as each dial approaches and reaches the target.
- Haptics use the XR controller's impulse capability. bHaptics wearable output is not wired into this first pass.

## Scene setup

1. In Unity, choose **Tools > VR Puzzle > Build Dual Dial Haptic Scene**. This creates `Assets/Scenes/DualDialHapticPuzzle.unity` and adds it to Build Settings while preserving `CrowbarProjectionVR`.
2. The builder places two dial pivots in front of the player, assigns the XR tracking-origin transform, and sets each dial to the left or right controller.
3. Open the generated scene and review the dial distance and height for the play area. Connect `puzzleCompleted` to a door, sound, or completion animation using the Inspector if desired.
4. Run the scene on Quest. Hold a controller's grip button and move it around the corresponding dial center. If the rotation direction feels reversed, toggle `invertRotation` on that dial.

## First-pass limitations

- This version uses controller position and the grip button; it does not yet use Meta hand tracking, physical grab colliders, or bHaptics wearables.
- Visual feedback is optional and currently changes a material color. Add a world-space instruction panel or progress display after the interaction is calibrated.
- The current scene setup and controller tracking origin must be assigned in Unity before this runs in VR.
