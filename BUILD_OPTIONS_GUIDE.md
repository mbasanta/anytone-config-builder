# BuildOptions Configuration Guide

This document provides detailed explanations of all BuildOptions enum values and their effects on the generated configuration.

## Overview

BuildOptions control how the builder transforms input CSVs into output CPS files. There are three independent configuration dimensions:

1. **SortMode** – How zones and talkgroups are ordered
2. **HotspotTxPermitMode** – TX permission for hotspot/simplex digital channels
3. **NicknameMode** – Whether repeater channels get location nicknames

## SortMode: Zone/Talkgroup Ordering

Controls how zones and talk groups are arranged in the output.

### Alpha (Default)

**Behavior**: Sort all zones and talk groups alphabetically by name

**Use case**: You want CPS to auto-organize your configuration alphabetically (easiest to navigate)

**Example**:
```
Input zones: [Repeaters, Analog, Simplex]
Output zones: [Analog, Repeaters, Simplex]  # Alphabetical order
```

**Pros**:
- Easy to find zones in CPS (alphabetical)
- Consistent ordering regardless of input order
- Simplest option (good for beginners)

**Cons**:
- Ignores your deliberate input ordering
- May not group related channels together

### RepeatersFirst

**Behavior**: Maintain your input order, BUT move DMR repeater channels to the front of each zone

**Use case**: You want to keep your custom zone ordering, but put repeaters at the top of each zone for easy access

**Detailed behavior**:
1. Zones appear in input order (first zone in input = first zone in output)
2. Within each zone, repeater channels appear before simplex/hotspots
3. Channel order within repeater/simplex groups preserves input order

**Example**:
```
Input:
  Zone "Test"
    - Simplex A (digital hotspot)
    - Repeater B (DMR repeater)
    - Repeater C (DMR repeater)
    - Simplex D (digital hotspot)

Output Zone "Test":
    - Repeater B (appears first)
    - Repeater C
    - Simplex A (appears after repeaters)
    - Simplex D
```

**Pros**:
- Preserves your custom zone ordering
- Repeaters grouped at top (easy access)
- Balances organization with custom intent

**Cons**:
- More complex than Alpha (not alphabetical)
- Requires input file to be pre-organized by zone

### AnalogFirst

**Behavior**: Maintain your input order, BUT move analog channels to the front of each zone

**Use case**: You're running mixed analog/digital and want analog channels prominent

**Detailed behavior**:
1. Zones appear in input order
2. Within each zone, analog channels appear before digital channels
3. Channel order within analog/digital groups preserves input order

**Example**:
```
Input:
  Zone "Test"
    - Digital Repeater A
    - Analog FM B
    - Digital Repeater C
    - Analog FM D

Output Zone "Test":
    - Analog FM B (appears first)
    - Analog FM D
    - Digital Repeater A (appears after analog)
    - Digital Repeater C
```

**Pros**:
- Preserves zone ordering from input
- Analog channels prominent (legacy systems, monitoring)
- Flexible for mixed-mode operations

**Cons**:
- Not alphabetical (harder to find by name)
- Requires knowing your input order

## HotspotTxPermitMode: Hotspot TX Permission

Controls whether hotspots and simplex digital channels can transmit on received channels.

### SameColorCode (Default)

**Behavior**: Hotspots may only transmit when receiving the same color code they're configured for

**Technical**: Sets `Same Color Code` in TX Permit field

**Use case**: You want to prevent accidental interference on other color codes (conservative, safer)

**Example**:
```
Hotspot configured with Color Code 1
Receive CC1 on frequency → Can transmit ✓
Receive CC2 on frequency → Cannot transmit ✗
```

**Pros**:
- Prevents cross-talk between color codes
- Reduces accidental interference
- Safer default for shared frequencies

**Cons**:
- May prevent legitimate cross-color-code operations
- Less flexible for dual-color-code hotspots

### Always

**Behavior**: Hotspots may always transmit, regardless of received color code

**Technical**: Sets `Always` in TX Permit field

**Use case**: You want hotspots to transmit on any received signal (permissive, flexible)

**Example**:
```
Hotspot with Any CC1, CC2, CC3, ...
Receive any color code → Can transmit ✓
```

**Pros**:
- Maximum flexibility
- Works with multiple color codes on same channel
- Good for monitoring/cross-linking scenarios

**Cons**:
- May cause interference if hotspot misconfigured
- Less protection against accidental transmissions

## NicknameMode: Repeater Channel Nicknames

Controls whether repeater channel names get location nicknames appended/prepended to avoid duplicates.

### Off (Default)

**Behavior**: Use repeater names exactly as provided in input

**Use case**: Your repeater names are already unique and short

**Example**:
```
Input repeaters:
  - Repeater W5ABC (multiple channels for different talkgroups)
  
Output channels:
  - Repeater W5ABC [TS1] (Channel 1)
  - Repeater W5ABC [TS1] (Channel 2)
  - Repeater W5ABC [TS2] (Channel 3)
```

**Pros**:
- No modifications to names
- Short, clean channel names
- Fastest processing

**Cons**:
- Duplicate names in CPS (hard to distinguish)
- May cause CPS issues if it expects unique names

### Prefix (If Needed)

**Behavior**: Add location nickname as prefix ONLY if duplicate names exist

**Technical**: Checks if multiple channels share same name; if yes, prepends location

**Use case**: Most repeaters are unique, but a few have duplicates

**Example**:
```
Input:
  Dallas Repeater (1000 channels) – Unique
  Austin Repeater (1000 channels) – Unique
  ARRL Simplex (100 channels) – Duplicate across both
  
Output:
  Dallas Repeater (no change, unique)
  Austin Repeater (no change, unique)
  Dallas-ARRL Simplex (prefix added)
  Austin-ARRL Simplex (prefix added)
```

**Pros**:
- Minimal modification (only when needed)
- Keeps most names short
- Smart duplicate detection

**Cons**:
- Mixed naming conventions (some with prefix, some without)
- May not catch all duplicates across different zones

### Suffix (If Needed)

**Behavior**: Add location nickname as suffix ONLY if duplicate names exist

**Technical**: Same as Prefix, but appends instead of prepends

**Example**:
```
Output:
  Repeater W5ABC (Dallas) (suffix added if duplicate)
  Repeater W5ABC (Austin) (suffix added if duplicate)
```

**Pros**:
- Suffix preserves repeater name at start (easier to scan)
- Same smart duplicate detection as Prefix

**Cons**:
- Longer names than Prefix mode
- May exceed CPS name length limits

### PrefixForced

**Behavior**: ALWAYS add location nickname as prefix, even for unique names

**Use case**: You want consistent naming convention with location prefixes

**Example**:
```
Output:
  Dallas-Repeater W5ABC (all names have Dallas- prefix)
  Dallas-ARRL Simplex (all names have Dallas- prefix)
```

**Pros**:
- Consistent naming convention
- All names have location prefix (easier to identify zone)
- Clear organization

**Cons**:
- Longer names (may exceed CPS limits)
- Extra prefix added even for already-unique names
- Takes up more CPS screen space

### SuffixForced

**Behavior**: ALWAYS add location nickname as suffix

**Use case**: You want consistent location suffix on all channels

**Example**:
```
Output:
  Repeater W5ABC (Dallas) (all names have suffix)
  ARRL Simplex (Dallas) (all names have suffix)
```

**Pros**:
- Consistent suffix on all channels
- Preserves original name at start

**Cons**:
- Longer names (may hit CPS limits)
- Extra suffix even for already-unique names

## Configuration Examples

### Example 1: Beginner Setup
```
SortMode: Alpha
HotspotTxPermitMode: SameColorCode
NicknameMode: Off
```
**Best for**: Users new to the tool, simple configurations, alphabetical organization

### Example 2: Preserve Input Organization
```
SortMode: RepeatersFirst
HotspotTxPermitMode: SameColorCode
NicknameMode: Prefix (If Needed)
```
**Best for**: Users with carefully organized input files, repeater-heavy configs

### Example 3: Conservative (Safe) Setup
```
SortMode: Alpha
HotspotTxPermitMode: SameColorCode
NicknameMode: PrefixForced
```
**Best for**: Complex networks, multiple locations, safety-conscious operators

### Example 4: Flexible Digital Setup
```
SortMode: Alpha
HotspotTxPermitMode: Always
NicknameMode: SuffixForced
```
**Best for**: Digital-heavy, multi-location, cross-linking operations

## Interacting Options

These options are **independent** – you can mix and match freely:

| SortMode | HotspotTxPermitMode | NicknameMode | Use Case |
|----------|---------------------|--------------|----------|
| Alpha | SameColorCode | Off | Default/safe |
| Alpha | SameColorCode | PrefixForced | Multi-location + alphabetical |
| RepeatersFirst | SameColorCode | Prefix (If Needed) | Preserve input + smart nicknames |
| AnalogFirst | Always | SuffixForced | Analog-first + flexible TX + consistent naming |

## CPS Constraints & Implications

### Name Length Limit: 16 Characters

All channel names are limited to 16 characters in CPS. With nicknames:

**Off**: `Repeater W5ABC` (14 chars) ✓
**Prefix (If Needed)**: `Dallas-Repeater` (15 chars) ✓
**PrefixForced**: `Austin-Repeater W` (17 chars) ✗ EXCEEDS LIMIT

To avoid exceeding limits:
- Use shorter original names
- Use "If Needed" mode instead of "Forced"
- Use 2-3 letter location codes (DA, AUS, etc.)

### Duplicate Channel Prevention

CPS may silently fail or misbehave with duplicate names. The builder helps:
- **Off** mode: Detects potential duplicates in validation warnings
- **Prefix/Suffix (If Needed)**: Automatically fixes most duplicates
- **PrefixForced/SuffixForced**: Guarantees unique names (within length constraints)

### Zone & Scanlist Limits

CPS supports:
- 250+ zones
- 1000+ channels per zone
- 250+ scanlists

All sort modes respect these limits.

## Troubleshooting

### "Channel names exceed 16 characters"
**Issue**: Used PrefixForced/SuffixForced with long original names
**Solution**: 
- Switch to "If Needed" mode
- Shorten original repeater names
- Use location abbreviations (DA instead of Dallas)

### "I don't see my zones in alphabetical order"
**Issue**: Using RepeatersFirst/AnalogFirst mode (preserves input order)
**Solution**: Switch to Alpha mode for alphabetical ordering

### "Hotspots can't transmit"
**Issue**: Using SameColorCode mode with different hotspot color codes
**Solution**: Change to Always mode

### "Too many duplicate names"
**Issue**: Using Off mode with many similar channel names
**Solution**: Use "If Needed" or "Forced" nickname modes

---

**Last Updated**: May 5, 2026  
**Version**: 1.0
