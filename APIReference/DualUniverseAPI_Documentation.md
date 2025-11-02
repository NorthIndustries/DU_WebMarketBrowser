# Dual Universe Server API Documentation

## Overview

This documentation covers the Dual Universe game server API, focusing on player location tracking, construct hierarchy, and PVP zone detection. The API uses Orleans grains for distributed computing and provides both synchronous and asynchronous operations.

## Core Concepts

### Construct Hierarchy System

Dual Universe uses a hierarchical system where:

- **ID 0**: The entire universe (root)
- **IDs 1-100**: Planets and celestial bodies
- **IDs 100+**: Player-built constructs (ships, stations, etc.)

Constructs can be parented to other constructs, creating a tree structure. Players are always parented to a construct.

### PVP Zone System

The game has several zone types:

- **Safe Zones**: Areas where PVP is disabled (around planets, starter areas)
- **PVP Zones**: Areas where player vs player combat is allowed
- **Interdiction Zones**: Special areas around alien cores and asteroids

## Key Data Structures

### Player Information

```javascript
// From CPPMod.playerInfo()
{
    "transform": "4x3 local transform matrix",
    "worldTransform": "4x3 world transform matrix",
    "parentId": "construct player is parented to",
    "velocity": "3d vector of current local velocity",
    "centerOfMass": "3d world vector of center of mass coordinates",
    "jetpack": "boolean - is jetpack on",
    "sprint": "boolean - is sprinting",
    "grounded": "boolean - is grounded",
    "torch": "boolean - is flashlight on",
    "gameTime": "total game time in seconds",
    "currentTool": "current equipped tool Item Hierarchy name",
    "playerId": "current player id"
}
```

### Construct Information

```csharp
struct ConstructUpdate {
    ConstructUid constructId;
    ConstructUid baseId;        // Parent construct ID
    Vec3 position;              // Position relative to parent
    Quat rotation;              // Rotation relative to parent
    Vec3 worldRelativeVelocity;
    Vec3 worldAbsoluteVelocity;
    Vec3 worldRelativeAngVelocity;
    Vec3 worldAbsoluteAngVelocity;
    uint64 pilotId;
    bool grounded;
    TimePoint time;
}
```

### Location Data

```csharp
struct RelativeLocation {
    ConstructUid constructId;   // The construct where the body is
    Vec3 position;              // Position in the construct frame
    Quat rotation;              // Rotation in the construct frame
}
```

## API Methods for PVP Zone Detection

### Method 1: Direct Construct Zone Check (Recommended)

```csharp
// Check if a construct is in a safe zone
IConstructGrain constructGrain = grainFactory.GetConstructGrain(constructId);
bool isInSafeZone = await constructGrain.IsInSafeZone();

// Check PVP timer status
IConstructFightGrain fightGrain = grainFactory.GetConstructFightGrain(constructId);
bool isInPvp = await fightGrain.IsInPvp();
TimePoint pvpTimerEnd = await fightGrain.GetPvpTimerEnd();
```

### Method 2: Position-Based Zone Check

```csharp
// Get planetary safe zones
IDirectServiceGrain directService = grainFactory.GetDirectServiceGrain();
SphereList planetarySafeZones = await directService.GetPlanetarySafeZones();

// Check interdiction zones
IZonesGrain zonesGrain = grainFactory.GetZonesGrain();
bool isInInterdictionZone = await zonesGrain.IsInInterdictionZone(worldPosition);

// Check safe zones via planet service
IPlanetList planetList; // Injected service
bool isInSafeZone = planetList.IsInSafeZone(worldPosition);
bool isInPlanetarySafeZone = planetList.IsInPlanetarySafeZone(worldPosition);
```

### Method 3: Player Location Tracking

```csharp
// Get player location
IScenegraph scenegraph; // Injected service
RelativeLocation playerLocation = await scenegraph.GetPlayerLocation(playerId);
(RelativeLocation local, RelativeLocation world) = await scenegraph.GetPlayerWorldPosition(playerId);

// Get player info via grain
IPlayerGrain playerGrain = grainFactory.GetPlayerGrain(playerId);
// Player grain methods for location tracking
```

## Recommended PVP Zone Detection Flow

Based on your requirements, here's the recommended approach:

### Step 1: Get Player's Parent Construct

```javascript
// Using JavaScript API in mods
let playerInfo = JSON.parse(CPPMod.playerInfo());
let parentConstructId = playerInfo.parentId;
```

```csharp
// Using C# server-side
IScenegraph scenegraph;
RelativeLocation playerLocation = await scenegraph.GetPlayerLocation(playerId);
ConstructUid parentConstructId = playerLocation.constructId;
```

### Step 2: Check Construct Hierarchy

```csharp
// Get the construct tree to understand the hierarchy
IScenegraphGrain scenegraphGrain = grainFactory.GetScenegraphGrain();
ConstructTree tree = await scenegraphGrain.GetConstructTree(parentConstructId);

// Walk up the hierarchy to find the root parent
ConstructUid rootParent = parentConstructId;
foreach(var construct in tree.constructs) {
    if(construct.parentId == 0) { // Universe root
        rootParent = construct.constructId;
        break;
    }
}
```

### Step 3: Determine PVP Status

```csharp
public async Task<bool> IsPlayerInPvpZone(PlayerId playerId) {
    // Get player's current construct
    var playerLocation = await scenegraph.GetPlayerLocation(playerId);
    var constructId = playerLocation.constructId;

    // Method 1: Direct construct check (fastest)
    var constructGrain = grainFactory.GetConstructGrain(constructId);
    bool isInSafeZone = await constructGrain.IsInSafeZone();

    if (isInSafeZone) {
        return false; // Definitely not PVP
    }

    // Method 2: Check if on a planet (IDs 1-100)
    if (constructId.constructId >= 1 && constructId.constructId <= 100) {
        return false; // On a planet, generally safe
    }

    // Method 3: Check world position against safe zones
    var (local, world) = await scenegraph.GetPlayerWorldPosition(playerId);
    bool isInPlanetarySafeZone = planetList.IsInPlanetarySafeZone(world.position);

    if (isInPlanetarySafeZone) {
        return false; // In planetary safe zone
    }

    // Method 4: Check PVP timer status
    var fightGrain = grainFactory.GetConstructFightGrain(constructId);
    bool hasActivePvpTimer = await fightGrain.IsInPvp();

    // If not in safe zone and not on planet, likely PVP
    return true;
}
```

## JavaScript Mod API

For client-side mods, you have limited but useful APIs:

```javascript
// Get current player info
let playerInfo = JSON.parse(CPPMod.playerInfo());
console.log("Player is on construct:", playerInfo.parentId);

// Get avatar info for other players
let avatarInfo = JSON.parse(CPPMod.avatarInfo(playerId));
console.log("Avatar is on construct:", avatarInfo.parentId);

// Raycast to get construct information
let raycastResult = JSON.parse(CPPMod.raycast());
console.log("Hit construct:", raycastResult.constructId);
```

## Configuration and Rules

### Safe Zone Configuration

From `gameplayRules.yaml`:

```yaml
planetsAreSafeZones: true
planetSafeZoneFallbackRadius: 500000.0 # 500km
pvpTimerDuration: 900 # 15 minutes in seconds
```

### Interdiction Zones

```yaml
gameplayInterdictionZoneRadius: 50000 # 50km
alienInterdictionZoneRadius: 1000000 # 1000km
asteroidInterdictionZoneRadius: 100000 # 100km
```

## Error Handling

Common error codes related to zones:

- `ConstructInInterdictionZone = 266`
- `BaseShieldPvpTimerIsActive = 1533`
- `CannotTokenizePvpTimerActive = 1551`
- `CannotFightInLockdown = 1552`

## Performance Considerations

1. **Cache Results**: Zone status doesn't change frequently, cache results for a few seconds
2. **Use Direct Methods**: `IConstructGrain.IsInSafeZone()` is faster than position calculations
3. **Batch Operations**: Use batch methods when checking multiple players
4. **Hierarchy Awareness**: Remember that constructs can be nested, check the root parent

## Summary

Your assumption about the hierarchy system is correct. The most efficient way to determine if a player is in a PVP zone is:

1. Get the player's parent construct ID
2. Check if that construct is in a safe zone using `IConstructGrain.IsInSafeZone()`
3. If not in safe zone, check if it's on a planet (ID 1-100)
4. For space constructs (ID 100+), check world position against safe zone spheres
5. Consider PVP timer status for recent combat activity

The API provides multiple approaches, but the direct construct-based checks are most efficient and reliable.
