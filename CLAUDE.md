# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**happygamefps** is a Unity 6 (6000.4.5f1) first-person shooter game built with C# and the Universal Render Pipeline (URP). It is structured as a tutorial-based learning game with a modular architecture separating core gameplay, AI, UI, and game systems.

## Tech Stack

- **Engine**: Unity 6.0.4.5f1 (latest LTS)
- **Language**: C# with .NET Standard 2.1
- **Rendering**: Universal Render Pipeline (URP) 17.4.0
- **Input System**: New Input System 1.19.0
- **Navigation**: NavMesh with AI Navigation 2.0.12
- **Build System**: .slnx solution file (DreamGameFps.slnx) with 8 C# assemblies

## Project Structure

Assets/FPS/ contains 79 C# scripts organized into:
- **Game/** - Core framework (managers, events, shared systems)
- **Gameplay/** - Player mechanics, weapons, pickups, objectives
- **AI/** - Enemy controllers, detection, navigation
- **UI/** - HUD, menus, notifications

Plus Scenes/, Animation/, Audio/, Art/, Prefabs/, Tutorials/
ModAssets/ has additional content; NavMeshComponents/ has custom navigation helpers.

## Architecture & Core Systems

### 1. Event System (Foundation)

**File**: Game/Managers/EventManager.cs

Custom pub/sub event system using generic GameEvent classes. All game communication flows through this system instead of direct references.

Key events:
- ObjectiveUpdateEvent
- GameOverEvent
- PlayerDeathEvent
- EnemyKillEvent (with remaining count)
- PickupEvent
- DamageEvent
- DisplayMessageEvent

**Pattern**: Systems broadcast events; listeners subscribe via EventManager.AddListener<T>()

### 2. Manager Classes (Singleton-like orchestrators)

Located in Game/Managers/ and Gameplay/Managers/:

- GameFlowManager: Controls game start/end, scene transitions, win/lose states, fade effects
- EventManager: Central event bus
- ActorsManager: Tracks all actors (player + enemies), maintains affiliation/team system
- EnemyManager: Manages enemy lifecycle, kill events
- PlayerInputHandler: Input abstraction layer using New Input System
- PlayerWeaponsManager: Weapon switching, aiming, firing logic, ammo management
- ObjectiveManager: Objective tracking and completion
- AudioManager: Master audio volume control
- GameObjectPoolManager: Generic GameObject object pool (see Object Pooling section)

### 3. Player Systems (Gameplay/)

- PlayerCharacterController: Movement, jumping, crouching, sprinting, gravity
  - Uses CharacterController component
  - Camera height adjusts based on stance (standing 1.8m to crouching 0.9m)
  - Footstep sounds tied to movement distance
  
- PlayerWeaponsManager: Weapon slot management, FOV changes while aiming, recoil animation
- PlayerInputHandler: Input abstraction (keyboard, gamepad, mouse)
- Jetpack: Special movement mechanic with fuel/recharge

### 4. Weapon System (Game/Shared/WeaponController.cs)

- **Types**: Manual, Automatic, Charge-based firing
- **Features**: Ammo clips, reload mechanics, projectile spread, recoil, crosshair system
- **Physics**: Physical bullet ejection with shell casing pools
- **Integration**: Crosshair updates based on target (enemy vs empty)
- Weapons stored as list in PlayerWeaponsManager with active index tracking

### 5. AI System (AI/)

- EnemyController: Base enemy class with health, visual feedback, death/loot system
- EnemyManager: Tracks enemy count, broadcasts kill events
- **Enemy Types**:
  - EnemyMobile: Navigation-based enemies using NavMesh
  - EnemyTurret: Stationary enemies
- DetectionModule: Line-of-sight + distance-based aggression
- NavigationModule: Path following with reaching radius, orientation smoothing
- PatrolPath: Waypoint system for enemy patrols

### 6. Gameplay Systems

- **Damage/Health**: Health.cs, Damageable.cs - Shared damage interface
- **Destructibles**: Destructable.cs - Breakable objects with VFX
- **Pickups**: AmmoPickup, HealthPickup, WeaponPickup - Item collection
- **Objectives**: Base Objective class with 3 types:
  - ObjectiveKillEnemies
  - ObjectivePickupItem
  - ObjectiveReachPoint
- **Projectiles**: ProjectileBase.cs, ProjectileStandard.cs - Base and specialized implementations

### 7. UI System (UI/)

- **HUD Managers**: AmmoCounter, WeaponHUDManager, EnemyCounter, JetpackCounter, ObjectiveHUDManager
- **Feedback**: FeedbackFlashHUD (damage), NotificationHUDManager, DisplayMessageManager
- **Navigation**: MenuNavigation, InGameMenuManager for pause menu
- **Crosshair**: CrosshairManager with dynamic switching
- **Compass**: Navigation aid with markers

## C# Assembly Organization

Solution splits into 8 assemblies:
1. **fps.Game** - Core framework
2. **fps.AI** - Enemy AI
3. **fps.Gameplay** - Player mechanics and objectives
4. **fps.UI** - UI systems
5. **NavMeshComponents** - Custom navigation helpers
6. **NavMeshComponentsEditor** - Editor tools
7. **Unity.FPS.Tutorials** - Tutorial content
8. **Unity.Microgame.Tutorials** - Additional tutorials

## Building & Development

### Opening the Project
- Requires Unity 6.0.4.5f1 (check ProjectSettings/ProjectVersion.txt)
- Open via .slnx file in Visual Studio or Unity Editor
- No external package manager needed; dependencies handled by Unity

### Running the Game
- Press Play in Unity Editor
- Main playable scenes: MainScene, SecondaryScene
- Entry point: IntroMenu scene

### Key Scenes
- **IntroMenu.unity** - Menu with LoadSceneButton
- **MainScene.unity** - Primary level (191KB)
- **SecondaryScene.unity** - Secondary level (39MB)
- **WinScene.unity** - Victory screen
- **LoseScene.unity** - Defeat screen

### Testing
com.unity.test-framework 1.6.0 included.
Run via: Window → Testing → Test Runner

### Input Mapping (GameConstants.cs)
- **Vertical/Horizontal**: WASD or analog sticks
- **Fire**: Left Mouse / Gamepad Trigger
- **Aim**: Right Mouse / Gamepad Shoulder
- **Sprint**: Left Shift / Gamepad Button
- **Jump**: Space / Gamepad Button
- **Crouch**: Ctrl / Gamepad Button
- **Reload**: R
- **Switch Weapon**: Mouse Wheel / Gamepad Button
- **Pause**: Esc

## Coding Conventions & Patterns

### Namespaces
All code under Unity.FPS.*:
- Unity.FPS.Game - Framework layer
- Unity.FPS.Gameplay - Gameplay mechanics
- Unity.FPS.AI - Enemy AI
- Unity.FPS.UI - User interface

### MonoBehaviour Patterns
- Use FindAnyObjectByType<T>() for manager lookups in Start()
- Register/unregister with managers in Awake()/OnDestroy()
- All configuration exposed as public [Tooltip(...)] fields for Inspector tweaking

### Actor System
All game entities (player, enemies) derive from Actor class:
- **Affiliation**: int-based team system (same = friendly)
- **AimPoint**: Transform for AI targeting reference
- Registered with ActorsManager for global awareness

### Input Abstraction
Input logic decoupled via PlayerInputHandler:
- Accessed by PlayerWeaponsManager, PlayerCharacterController
- Enables controller/keyboard/mouse agnostic gameplay

### Audio
- AudioUtility.cs: Master volume control, distance-based audio
- Uses Unity's AudioSource components; no third-party plugins

## Dependencies & Packages

Key Unity packages (Packages/manifest.json):
- com.unity.render-pipelines.universal - URP rendering
- com.unity.ai.navigation - AI pathfinding
- com.unity.inputsystem - New Input System
- com.unity.probuilder - Level building tools
- com.unity.test-framework - Testing
- com.unity.learn.iet-framework - Tutorial framework

All via Package Manager; no external NuGet.

## Object Pooling

**Files**: `Game/Managers/GameObjectPoolManager.cs`, `Game/PooledObject.cs`, `Game/PooledParticleAutoRelease.cs`

`GameObjectPoolManager` is a lazy-created singleton (auto-instantiates on first use, no scene setup needed). It wraps Unity's `ObjectPool<GameObject>` with a per-prefab dictionary keyed by `GetInstanceID()`.

**API**:
- `GameObjectPoolManager.Instance.Get(prefab, position, rotation)` — get from pool (or instantiate)
- `GameObjectPoolManager.Instance.Release(instance)` — return to pool via `PooledObject` component
- `GameObjectPoolManager.Instance.ReleaseDelayed(instance, delay)` — timed release via coroutine

**How pooled objects work**:
- On `Get`: object is unparented, positioned in world space, activated
- On `Release`: object is re-parented under `GameObjectPoolManager` in hierarchy, deactivated (appears nested/hidden, not at scene root)
- `PooledObject` component (added automatically) stores the `PrefabId` so any instance can return itself to the correct pool

**Particle VFX auto-release**:
- If the prefab has a `ParticleSystem` (checked via `GetComponentInChildren`), `PooledParticleAutoRelease` is added automatically
- It replays particles on `OnEnable` and returns the object to pool when `IsAlive` goes false — no manual lifetime management needed

**Currently pooled**:
- Impact VFX (e.g. `VFX_LazerSparksRed`) — auto-released by `PooledParticleAutoRelease`
- Muzzle flash — auto-released by `PooledParticleAutoRelease` if has ParticleSystem, else `ReleaseDelayed(2f)`
- Projectiles (`ProjectileBase` subclasses) — returned on hit or max lifetime; `OnDisable` unsubscribes `OnShoot` to prevent accumulation on reuse

**Projectile-specific notes**:
- `m_Returned` flag prevents double-release if hit and max lifetime fire simultaneously
- `Physics.SphereCastNonAlloc` with static `s_HitBuffer[16]` — no per-frame array allocation
- `m_IgnoredColliders` list is reused (`Clear()`) rather than reallocated each shot

**Shell casings** use a separate `Queue<Rigidbody>` pool inside `WeaponAmmoModule` (predates `GameObjectPoolManager`).

## Performance Considerations

- **Mesh Combining** (MeshCombiner.cs): Reduces draw calls for static geometry
- **Object Pooling**: GameObjectPoolManager pools projectiles, VFX, and muzzle flashes — see Object Pooling section
- **Shell Casing Pooling**: ShellPoolSize parameter in WeaponAmmoModule prevents allocations
- **Physics Layers**: Specific GroundCheckLayers for efficient raycasts
- **NavMesh**: Pre-baked for static levels; PathReachingRadius tunable per enemy
- **NonAlloc Physics Pattern**: All overlap and raycast calls use NonAlloc variants with static buffers to eliminate per-call GC allocations. Follow this convention for any new physics code:
  - `DetectionModule` — `static RaycastHit[] s_RaycastBuffer[16]` for line-of-sight checks
  - `DamageArea` — `static Collider[] s_OverlapBuffer[64]` for explosion radius
  - `PlayerCharacterController` — `static Collider[] s_StandingOverlapBuffer[8]` for crouch checks
- **Enemy Detection Throttle**: `EnemyController.Update()` calls `DetectionModule.HandleTargetDetection()` at most 10×/sec (interval 0.1s via `m_NextDetectionTime`). Detection is intentionally not frame-perfect — do not remove the throttle without profiling.
- **Camera.main Caching**: Never access `Camera.main` in `Update()` — it internally calls `FindWithTag`. Cache it in `Start()` (see `WorldspaceHealthBar`).
- **Body Flash Rendering**: `EnemyController` only calls `SetPropertyBlock` during the active hit-flash period (`m_FlashActive`). The flag is set in `OnDamaged` and cleared when the gradient completes (ratio ≥ 1).

## Extension Points

- **Objectives**: Subclass `Objective` base class; place in `Gameplay/Objectives/`
- **Enemy behaviors**: Add modules alongside DetectionModule + NavigationModule
- **Weapon types**: Extend `WeaponShootType` enum in WeaponController.cs
- **UI**: Subscribe to EventManager events rather than polling game state
