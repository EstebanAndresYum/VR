# Crowbar projection puzzle

`CrowbarProjectionPuzzle` compares two cut joints from the active viewpoint. Each side of a joint owns three anchors: center, horizontal and vertical. The anchors are projected with `Camera.WorldToViewportPoint`, and the average screen-space distance becomes the joint score.

The puzzle completes only when both joints are at least 97% aligned for 1.5 seconds. Using the lower joint score prevents one perfect joint from hiding a badly aligned joint.

Use **Tools > Crowbar Puzzle > Build Demo Scene** to generate:

- three non-destructive mesh segments from `Assets/Art/Crowbar/Models/crowbar_1.fbx`;
- paired projection anchors on both cuts;
- an assembled crowbar that replaces the fragments on completion;
- `Assets/Scenes/CrowbarProjectionDemo.unity`.

The demo camera uses WASD to move, Q/E for vertical movement, arrow keys to rotate and Shift to move faster. For VR, assign the HMD camera to the puzzle component and remove the desktop camera controller.
