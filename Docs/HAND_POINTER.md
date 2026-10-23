# Whole-hand menu pointer

Show either complete hand to the camera. Open, closed and partially bent fingers all use the same valid 21-landmark frames as Module 1; there is no index-finger-only gesture requirement.

The cursor follows the palm centre (landmarks 0, 5, 9, 13 and 17). Hold it over an enabled visible control for five seconds to select. Move deliberately after selecting, or briefly remove the hand, before another selection. Mouse and keyboard controls remain available.

In Unity Editor, focus Game during Play. Background input, stale frames and invalid tracking cannot activate controls. The event cache reconstructs missing managed PointerEventData even when Unity's EventSystem reference is unchanged, preventing the previous repeated null-reference fault after script reload.

Software fixtures do not establish real-hand usability. The current follow-up checklist is [TASKS.md](TASKS.md); native Editor and Windows validation must be rerun for the current source revision.
