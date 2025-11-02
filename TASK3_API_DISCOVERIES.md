# Task 3: Orleans API Data Structure Discoveries

This document captures the key findings from Task 3 exploration of Orleans connection and market data structures. These discoveries are essential for implementing subsequent tasks.

## Orleans Connection Verification ✅

- **Orleans Client Type**: `ClusterClient`
- **Bot Authentication**: Successfully authenticated as Player ID 10049
- **Player Grain Access**: Confirmed working
- **Connection Status**: Stable and functional

## PlayerInfo Structure

**Type**: `PlayerInfo` with 7 properties

### Properties Discovered:

- `playerId`: `UInt64` - Unique player identifier
- `name`: `String` - Player display name
- `colors`: `String` - Player avatar colors (e.g., "blue")
- `gender`: `Boolean` - Player avatar gender
- `isAdmin`: `Boolean` - Admin status flag
- `title`: `String` - Player title (can be empty)
- `properties`: `Dictionary<String, PropertyValue>` - Additional player properties

### Sample Data:

```
Bot Player: marketBrowser (ID: 10049)
Other Player: marketbot (ID: 3, Admin: true)
```

## MarketInfo Structure

**Type**: `MarketInfo` with 14 properties

### Key Properties:

- `marketId`: `UInt64` - Unique market identifier
- `name`: `String` - Market display name
- `relativeLocation`: `RelativeLocation` - Market location reference
- `creatorName`: `String` - Market creator (e.g., "Aphelia")
- `capacity`: `UInt64` - Market storage capacity
- `valueTax`: `Double` - Market value tax rate
- `dailyStorageFee`: `Double` - Daily storage fee
- `orderFee`: `Double` - Order placement fee
- `creationDate`: `TimePoint` - Market creation timestamp
- `allowedItemTypes`: `List<UInt64>` - Allowed item type IDs

### Market Discovery:

- **Total Markets Found**: 125 markets per planet
- **Tested Planets**: 2 (Alioth), 26, 27, 30, 31 - all have 125 markets
- **Sample Markets**: "Market Sanctuary 09", "Market Sanctuary 08", "Market Sanctuary 07"
- **Construct Info**: Successfully retrievable with position data

## MarketOrder Structure

**Type**: `MarketOrder` with detailed order information

### Key Properties:

- `orderId`: `UInt64` - Unique order identifier
- `marketId`: `UInt64` - Associated market ID
- `itemType`: `UInt64` - Item type identifier
- `buyQuantity`: `Int64` - Order quantity (positive = buy, negative = sell)
- `unitPrice`: `Currency` - Price per unit
- `ownerId`: `EntityId` - Order owner entity
- `ownerName`: `String` - Order owner display name
- `expirationDate`: `TimePoint` - Order expiration
- `updateDate`: `TimePoint` - Last update timestamp

### Order Type Detection:

- **Buy Orders**: `buyQuantity > 0`
- **Sell Orders**: `buyQuantity < 0` (negative quantity indicates sell)

### Market Activity Data:

- **Sample Market**: Market Sanctuary 09 (ID: 125)
- **Total Orders**: 1,135 orders
- **Buy Orders**: 1,046
- **Sell Orders**: 89 (detected via negative buyQuantity)
- **Unique Item Types**: 1,116 different items

## Item Definition Access

**Type**: `GameplayDefinition` via `IGameplayBank.GetDefinition(itemType)`

### Properties Available:

- `Id`: `UInt64` - Item type ID
- `Name`: `String` - Item display name
- `BaseObject`: Item base class (e.g., `NQutils.Def.DecorativeUnit`)
- `Type`: Item type classification
- `IsInDatabase`: `Boolean` - Database presence flag

### Sample Items Discovered:

- `AdjunctTop_02` (ID: 3337817674) - DecorativeUnit
- `AileronShortMedium2` (ID: 1923840124) - Airfoil
- `AileronShortSmall2` (ID: 2334843027) - Airfoil
- `AileronSmall2` (ID: 2292270972) - Airfoil
- `AlFeProduct` (ID: 18262914) - RefinedMaterial

## Construct Information Access

**Access Method**: `orleans.GetConstructInfoGrain(constructId).Get()`

### Available Data:

- **Position**: `Vec3` coordinates (e.g., -262152, -262152, -388447)
- **Construct Name**: (e.g., "Alioth")
- **Construct Data**: Full construct information available

## Key Technical Insights

### Market Data Patterns:

1. **Consistent Market Count**: All tested planets have exactly 125 markets
2. **Market Naming**: Follow pattern "Market Sanctuary XX"
3. **Creator**: Most markets created by "Aphelia"
4. **Fees**: Many markets have 0 fees (valueTax, dailyStorageFee, orderFee)

### Order Patterns:

1. **Order Distribution**: Heavy buy-side activity (1046 buy vs 89 sell orders)
2. **Item Diversity**: High variety (1116 unique items in single market)
3. **Price Range**: Wide price range from 11,000 to 873,700+ per unit
4. **Owner Distribution**: Mix of bot and player orders

### API Reliability:

1. **Orleans Connection**: Stable and consistent
2. **Data Retrieval**: Fast and reliable
3. **Cross-References**: Item definitions successfully linkable
4. **Construct Info**: Available for position/location data

## Implementation Recommendations

### For Next Tasks:

1. **Use existing bot session** rather than creating new ones (session timeout issue)
2. **Cache item definitions** to avoid repeated GameplayBank calls
3. **Handle negative buyQuantity** as sell orders in data models
4. **Leverage construct info** for market positioning
5. **Consider market capacity** for storage calculations
6. **Account for market fees** in profit calculations

### Data Model Updates Needed:

1. Update `MarketOrderData.SellQuantity` logic to handle negative `buyQuantity`
2. Add item name caching mechanism
3. Include market fee information in calculations
4. Add construct position data to market models

## Session Management Notes

- **Single Bot Limitation**: Creating multiple bot sessions causes InvalidSession errors
- **Recommendation**: Reuse the main bot session for all operations
- **Workaround**: Use the existing authenticated bot rather than creating separate service bots

---

_Generated from Task 3 execution on MarketBrowserMod_
_Date: Task 3 completion_
