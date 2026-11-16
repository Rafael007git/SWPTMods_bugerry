# Breast Physics (Enhanced Gravity & Linear Inertia Edition)

An extended **BepInEx** physics plugin for the game **She Will Punish Them** utilizing `DuckType.Jiggle` and Genesis 8F character rig.

Built upon the original **Breast Physics** mod by **bugerry**, this version introduces a secondary physics layer that adds **true world-space gravity**, **linear translational inertia**, and **dynamic upward blendshape deformation** while preserving the vanilla "hinge with adjusting rotation axis" response.

---

## ✨ Features

### Legacy / Core Features (Original Mod by bugerry87)
* **In-Game Customization UI:** Injects custom physics sliders (`Jelly`, `Spring`, `Hold`, `Mass`, `Angle`, `Limit`, `Gravity`, `Inertia`, `Free3D`) directly into the character customization menu.
* **Per-Character & Preset Persistence:** Automatically saves and loads individual physics profiles for each character and character preset via `ES2`.
* **Flexible Update Loop:** Supports multiple update modes (`Auto`, `Per Frame`, `Post Frame`, `On Physics Update`) to match different framerate setups.
* **Direct `DuckType.Jiggle` Tuning:** Exposes internal hinge angles, angular limits, soft limits, and center-of-mass parameters for `lPectoral` and `rPectoral` bones.

### New Physics Extensions (Gravity & Linear Inertia Addon)
* **Gravity with Standing Pose Compensation (`Vector3.down + chest.up`):**
  * Smoothly pulls and rotates the breasts along the world gravity vector when leaning forward, tilting sideways, lying down, or in inverted poses.
  * Cancels out downward gravity while standing upright so the pre-sculpted mesh retains its intended height and shape.
* **Linear Translational Inertia (Mass-Spring-Damper):** Simulates a virtual world-space mass attached to `chestLower` to generate smooth vertical bounce during steps, jumps, and sudden torso movements.
* **Hybrid Upward Blendshape Deformation (`Morph 74`):** Dynamically drives the custom upward rotation blendshape (`breast rotation updown`) during upward bounce and inverted poses for natural volume rounding without harsh bone creasing.
  * The previously unused morph `74- Genesis8Female__mine breast rotation updown` was found in the game model.
  * Smart Anti-Clipping Protection (`OnlyWhenTopless`): Automatically disables blendshape deformation whenever `armor` or `bra` slots are equipped on the character, preventing mesh clipping through tops and lingerie.

---

## ⚙️ Configuration

* **In-Game Sliders:** Core `DuckType.Jiggle` parameters can be adjusted inside the Character Customization screen and are saved per character.
* **Addon & Advanced Settings:** All new gravity, inertia, and blendshape parameters can be tweaked in real time via **BepInEx Configuration Manager** (press `F1` in-game) or edited in `BepInEx/config/bugerry.BreastPhysics.cfg`.

### `[General]`
* `Enabled` — Master toggle for the plugin.
* `Update Mode` — Physics update timing (`0 = Auto`, `1 = Per Frame`, `2 = Post Frame`, `3 = On Physics Update`).

### `[GravityAddon]` (New: Gravity & Linear Inertia)
* `EnableAddon` *(default: `true`)* — Enables the additive gravity and vertical inertia simulation.
* `CompensateStandingPose` *(default: `true`)* — Subtracts baseline gravity while standing upright to prevent double-sagging of the pre-sculpted mesh.
* `GravityStrength` *(default: `1.0`)* — Gravity influence multiplier during tilts and inverted poses (`0` = disabled).
* `InertiaStrength` *(default: `1.0`)* — Linear translational inertia multiplier (`0` = static gravity only, no bounce).
* `VerticalStiffness` *(default: `180.0`)* — Spring stiffness controlling return speed.
* `VerticalDamping` *(default: `10.0`)* — Oscillation damping factor.
* `MaxAngleDown` / `MaxAngleUp` / `MaxAngleSide` — Hard angular limits (in degrees) applied to `lPectoral` and `rPectoral` bones.
* `DisableVanillaJiggle` *(default: `false`)* — Disables the stock `DuckType.Jiggle` component (useful for isolated testing).

### `[Morph74Experiment]` (New: Upward Mesh Deformation)
* `EnableMorph74` *(default: `true`)* — Drives blendshape index `74` during upward movement.
* `OnlyWhenTopless` *(default: `true`)* — Restricts blendshape deformation to topless states (`!armor && !bra`) to avoid clothing clipping.
* `Morph74Sensitivity` *(default: `4.0`)* — Blendshape weight added per degree of upward pitch.
* `Morph74MaxDelta` *(default: `45.0`)* — Maximum additional blendshape weight added on top of the character's base customization value.

---

## 🗺️ Roadmap / Planned Features (ToDo)

* [ ] **Combined Gravity Roll & Anatomical Splay (`Cleavage` + `Lateral Roll`):**
  * **Technical Concept:** Combine two additive lateral components for `lPectoral` and `rPectoral`:
    1. *Direct Lateral Gravity Roll (`chest.right` vs `Vector3.down`)* — uniform sign for both breasts, pulling both toward the floor during side tilts.
    2. *Anatomical Ribcage Splay/Cleavage (`chest.forward` vs `Vector3.down`)* — mirrored sign (`+1` for left, `-1` for right) simulating how breasts rest on a curved ribcage.
  * **Expected Behavior:**
    * *Supine (lying on back, `chest.forward` pointing up):* Breasts symmetrically splay outward to the sides.
    * *Prone / Leaning Forward / All Fours (`chest.forward` pointing down):* Breasts pull inward toward the sternum, forming natural cleavage.
    * *Leaning + Side Twist:* The lower breast pulls outward toward the floor while the upper breast shifts inward across the chest.
* [ ] **Anatomical Collarbone (`lCollar` / `rCollar`) Coupling for `Morph 74`:**
  * **Technical Concept:** Read the local vertical elevation angles of `lCollar` and `rCollar` relative to `chestUpper` above their resting horizontal angle.
  * **Expected Behavior:** Raising the arms upward stretches the *pectoralis major* and proportionally increases `Morph 74` weight, naturally lifting the breasts (active only when topless, treating `armor`/`bra` as physical support).
* [ ] **Volume-Preserving Dynamic Bone Scaling (`Squash & Stretch`):**
  * **Technical Concept:** Apply subtle, volume-preserving `localScale` offsets (`s` along primary stretch axis, `1 / sqrt(s)` along perpendicular axes, clamped to `±3%–5%`) driven by vertical and longitudinal displacement to simulate soft-tissue compression.

---

## 🛠️ Installation

1. Ensure **BepInEx 5.x (x64)** is installed for your game.
2. Drop `BreastPhysics.dll` into your `BepInEx/plugins/` directory.
3. Launch the game once to generate the default configuration file.

---

## 🧩 Project Architecture

* **`BepInExPlugin.cs`** *(Core)* — Handles Harmony patches, injects custom sliders into `UICustomization`, manages `ES2` preset saving/loading, and hooks into `Jiggle.ScheduledUpdate`.
* **`BreastGravityAddon.cs`** *(Addon)* — Standalone static physics solver that computes additive quaternions in `chestLower` local space and selectively syncs blendshapes across character renderers.

---

## 🙏 Credits & Acknowledgments

* **[bugerry87](https://github.com/bugerry87)** — Original creator of the **Breast Physics** mod, UI slider integration, preset persistence system, and `DuckType.Jiggle` scheduling patches.
* **BepInEx & Harmony Teams** — For providing the essential modding framework and runtime patching tools for Unity.
* **Google Gemini** — Assisting with the physics architecture and C# implementation.