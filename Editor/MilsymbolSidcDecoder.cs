using System.Collections.Generic;

namespace Leuconoe.MilsymbolUnity.Editor
{
    /// <summary>
    /// Produces a human-readable description of a letter SIDC by decoding each builder
    /// field, e.g.
    /// <c>Warfighting/Friend/Air/Present/Unit/Air/Unmanned Aerial Vehicle/-/Squad/--/-</c>.
    ///
    /// Fields are separated by '/'. Single-char positions that are empty (a dash in the
    /// SIDC) render as '-', and the country code keeps its raw two characters, so the slash
    /// separator stays distinguishable from those dashes. The 6-char function id is split
    /// into Symbol Part / Domain / Specific Symbol by reverse-matching the known symbol
    /// table. Stored on <see cref="MilsymbolIconAsset"/> for quick reference.
    /// </summary>
    public static class MilsymbolSidcDecoder
    {
        private const char FieldSeparator = '/';
        private const char Empty = '-';

        private static readonly Dictionary<char, string> CodingSchemes = new Dictionary<char, string>
        {
            { 'S', "Warfighting" },
            { 'G', "Tactical Graphics" },
            { 'W', "METOC" },
            { 'I', "Signals Intelligence" },
            { 'O', "Stability Operations" },
            { 'E', "Emergency Management" }
        };

        private static readonly Dictionary<char, string> Affiliations = new Dictionary<char, string>
        {
            { 'P', "Pending" },
            { 'U', "Unknown" },
            { 'A', "Assumed Friend" },
            { 'F', "Friend" },
            { 'N', "Neutral" },
            { 'S', "Suspect" },
            { 'H', "Hostile" },
            { 'D', "Exercise Friend" },
            { 'J', "Joker" },
            { 'K', "Faker" }
        };

        private static readonly Dictionary<char, string> BattleDimensions = new Dictionary<char, string>
        {
            { 'P', "Space" },
            { 'A', "Air" },
            { 'G', "Ground" },
            { 'S', "Sea Surface" },
            { 'U', "Subsurface" },
            { 'F', "SOF" },
            { 'X', "Other" },
            { 'Z', "Unknown" }
        };

        private static readonly Dictionary<char, string> Statuses = new Dictionary<char, string>
        {
            { 'P', "Present" },
            { 'A', "Planned/Anticipated" },
            { 'C', "Fully Capable" },
            { 'D', "Damaged" },
            { 'X', "Destroyed" },
            { 'F', "Full To Capacity" }
        };

        private static readonly Dictionary<char, string> SymbolParts = new Dictionary<char, string>
        {
            { 'U', "Unit" },
            { 'E', "Equipment" },
            { 'I', "Installation" }
        };

        private static readonly Dictionary<char, string> SymbolDomains = new Dictionary<char, string>
        {
            { 'A', "Air" },
            { 'G', "Ground" },
            { 'S', "Sea Surface" },
            { 'U', "Subsurface" }
        };

        private static readonly Dictionary<char, string> ModifierOnes = new Dictionary<char, string>
        {
            { 'A', "Headquarters" },
            { 'E', "Task Force" },
            { 'F', "Feint/Dummy" },
            { 'H', "Installation" },
            { 'M', "Mobility" },
            { 'N', "Towed Array" }
        };

        private static readonly Dictionary<char, string> ModifierTwos = new Dictionary<char, string>
        {
            { 'A', "Team/Crew" },
            { 'B', "Squad" },
            { 'C', "Section" },
            { 'D', "Platoon/Detachment" },
            { 'E', "Company/Battery/Troop" },
            { 'F', "Battalion/Squadron" },
            { 'G', "Regiment/Group" },
            { 'H', "Brigade" },
            { 'I', "Division" },
            { 'J', "Corps/MEF" },
            { 'K', "Army" }
        };

        private static readonly Dictionary<char, string> OrderOfBattle = new Dictionary<char, string>
        {
            { 'A', "Air" },
            { 'E', "Electronic" },
            { 'C', "Civilian" },
            { 'G', "Ground" },
            { 'N', "Maritime" },
            { 'S', "Strategic Force" }
        };

        // Function id (6 chars) -> { generator code (Part/Domain), specific symbol label }.
        // Generator code position 0 is the symbol part, position 2 is the domain.
        private static readonly Dictionary<string, SymbolEntry> Symbols = new Dictionary<string, SymbolEntry>
        {
            { "MF----", new SymbolEntry("UUAA", "Aircraft, Fixed Wing") },
            { "MH----", new SymbolEntry("UUAB", "Aircraft, Rotary Wing") },
            { "MFQ---", new SymbolEntry("UUAC", "Unmanned Aerial Vehicle") },
            { "WM----", new SymbolEntry("UUAE", "Missile") },
            { "UCI---", new SymbolEntry("UUGA", "Infantry") },
            { "UCIZ--", new SymbolEntry("UUGB", "Mechanized Infantry") },
            { "UCIM--", new SymbolEntry("UUGC", "Motorized Infantry") },
            { "UCR---", new SymbolEntry("UUGE", "Reconnaissance") },
            { "UCF---", new SymbolEntry("UUGF", "Special Forces") },
            { "UCD---", new SymbolEntry("UUGJ", "Artillery") },
            { "UCDM--", new SymbolEntry("UUGM", "Mortar") },
            { "CLCV--", new SymbolEntry("UUSA", "Carrier") },
            { "CLCC--", new SymbolEntry("UUSB", "Cruiser") },
            { "CLDD--", new SymbolEntry("UUSC", "Destroyer") },
            { "CLFF--", new SymbolEntry("UUSD", "Frigate") },
            { "CLPT--", new SymbolEntry("UUSF", "Patrol Craft") },
            { "SN----", new SymbolEntry("UUUA", "Submarine") },
            { "SNB---", new SymbolEntry("UUUB", "Submarine Nuclear Ballistic Missile") },
            { "SNA---", new SymbolEntry("UUUC", "Submarine Nuclear Attack") },
            { "SC----", new SymbolEntry("UUUD", "Submarine Diesel Attack") }
        };

        private readonly struct SymbolEntry
        {
            public SymbolEntry(string generatorCode, string label)
            {
                GeneratorCode = generatorCode;
                Label = label;
            }

            public string GeneratorCode { get; }
            public string Label { get; }
        }

        /// <summary>A selectable sub-type appended after a specific symbol's base code.</summary>
        public readonly struct Variant
        {
            public Variant(string code, string label)
            {
                Code = code;
                Label = label;
            }

            /// <summary>Suffix appended after the base function id (e.g. "R" for MFQ -> MFQR--).</summary>
            public string Code { get; }
            public string Label { get; }
        }

        // UAV (function id base "MFQ") role sub-types, shared with the builder's Variant
        // dropdown so both produce/parse the same codes. Codes match milsymbol air.js.
        public static readonly Variant[] UavRoles =
        {
            new Variant("", "None"),
            new Variant("R", "Reconnaissance"),
            new Variant("A", "Attack"),
            new Variant("B", "Bomber"),
            new Variant("RW", "Airborne Early Warning"),
            new Variant("RZ", "Electronic Surveillance Measures"),
            new Variant("RX", "Photographic"),
            new Variant("C", "Cargo"),
            new Variant("D", "Airborne Command Post"),
            new Variant("F", "Fighter"),
            new Variant("H", "Combat Search and Rescue"),
            new Variant("J", "Jammer ECM"),
            new Variant("K", "Tanker"),
            new Variant("L", "VSTOL"),
            new Variant("M", "Special Operations Forces"),
            new Variant("I", "Mine Countermeasures"),
            new Variant("N", "Antisurface Warfare"),
            new Variant("P", "Patrol"),
            new Variant("S", "Antisubmarine Warfare"),
            new Variant("T", "Trainer"),
            new Variant("U", "Utility"),
            new Variant("Y", "Communications"),
            new Variant("O", "MEDEVAC")
        };

        // Manned aircraft (function id base "MF" fixed wing, "MH" rotary wing) role
        // sub-types. The role char sits right after the 2-char base.
        public static readonly Variant[] AirRoles =
        {
            new Variant("", "None"),
            new Variant("A", "Attack"),
            new Variant("B", "Bomber"),
            new Variant("F", "Fighter"),
            new Variant("R", "Reconnaissance"),
            new Variant("P", "Patrol"),
            new Variant("C", "Cargo"),
            new Variant("K", "Tanker"),
            new Variant("T", "Trainer"),
            new Variant("L", "VSTOL"),
            new Variant("J", "Jammer ECM"),
            new Variant("O", "MEDEVAC"),
            new Variant("H", "Personnel Recovery"),
            new Variant("D", "Airborne Command Post"),
            new Variant("U", "Utility"),
            new Variant("Y", "Communications"),
            new Variant("S", "Antisubmarine Warfare"),
            new Variant("M", "Special Operations Forces")
        };

        private static readonly Dictionary<string, string> UavRoleLabels = BuildRoleLabels(UavRoles);
        private static readonly Dictionary<string, string> AirRoleLabels = BuildRoleLabels(AirRoles);

        private static Dictionary<string, string> BuildRoleLabels(Variant[] roles)
        {
            var map = new Dictionary<string, string>();
            foreach (var role in roles)
            {
                if (role.Code.Length > 0)
                {
                    map[role.Code] = role.Label;
                }
            }

            return map;
        }

        // Ground unit / equipment function ids -> label. Part is derived from the leading
        // char (U=Unit, E=Equipment, I=Installation); compound parts join with '_'.
        private static readonly Dictionary<string, string> GroundFunctions = new Dictionary<string, string>
        {
            { "UCVU--", "Unmanned Ground Vehicle" },
            { "UUMSE-", "Electronic Warfare" },
            { "EWR---", "Rifle" },
            { "EWRR--", "Rifle_Short Range" },
            { "EWRL--", "Rifle_Intermediate Range" },
            { "EWRH--", "Rifle_Long Range" },
            { "EWT---", "Antitank Rocket Launcher" },
            { "EWTL--", "Antitank Rocket Launcher_Short Range" },
            { "EWTM--", "Antitank Rocket Launcher_Intermediate Range" },
            { "EWTH--", "Antitank Rocket Launcher_Long Range" },
            { "EWM---", "Missile Launcher" },
            { "EWMT--", "Antitank Missile Launcher" },
            { "EWS---", "Single Rocket Launcher" },
            { "EWX---", "Multiple Rocket Launcher" },
            { "EWZ---", "Grenade Launcher" },
            { "EWO---", "Mortar" },
            { "EWH---", "Howitzer" },
            { "EWG---", "Antitank Gun" },
            { "EWA---", "Air Defence Gun" }
        };

        /// <summary>
        /// Decodes a letter SIDC into a slash-joined description:
        /// Coding Scheme / Affiliation / Battle Dimension / Status /
        /// Symbol Part / Domain / Specific Symbol / Modifier 1 / Modifier 2 /
        /// Country / Order of Battle. Returns an empty string for null/blank input.
        /// </summary>
        public static string Describe(string sidc)
        {
            if (string.IsNullOrWhiteSpace(sidc))
            {
                return "";
            }

            var s = sidc.Trim().ToUpperInvariant();
            DecodeFunction(Substring(s, 4, 6), out var part, out var domain, out var symbol);

            var fields = new List<string>
            {
                Single(CodingSchemes, CharAt(s, 0)),
                Single(Affiliations, CharAt(s, 1)),
                Single(BattleDimensions, CharAt(s, 2)),
                Single(Statuses, CharAt(s, 3)),
                part,
                domain,
                symbol,
                Single(ModifierOnes, CharAt(s, 10)),
                Single(ModifierTwos, CharAt(s, 11)),
                Substring(s, 12, 2),
                Single(OrderOfBattle, CharAt(s, 14))
            };

            return string.Join(FieldSeparator.ToString(), fields);
        }

        private static void DecodeFunction(string functionId, out string part, out string domain, out string symbol)
        {
            if (functionId.Trim(Empty).Length == 0)
            {
                part = Empty.ToString();
                domain = Empty.ToString();
                symbol = Empty.ToString();
                return;
            }

            // UAV family: base "MFQ" + role suffix. Compound labels join with '_' so the
            // dash stays reserved for empty positions and '/' for field separators.
            if (functionId.StartsWith("MFQ", System.StringComparison.Ordinal))
            {
                part = "Unit";
                domain = "Air";
                symbol = "Unmanned Aerial Vehicle";

                var suffix = functionId.Length > 3 ? functionId.Substring(3).Trim(Empty) : "";
                if (suffix.Length > 0)
                {
                    symbol += "_" + (UavRoleLabels.TryGetValue(suffix, out var role) ? role : suffix);
                }

                return;
            }

            // Manned fixed/rotary wing: base "MF"/"MH" + role char.
            if (functionId.StartsWith("MF", System.StringComparison.Ordinal) ||
                functionId.StartsWith("MH", System.StringComparison.Ordinal))
            {
                part = "Unit";
                domain = "Air";
                symbol = functionId.StartsWith("MF", System.StringComparison.Ordinal) ? "Fixed Wing" : "Rotary Wing";

                var suffix = functionId.Length > 2 ? functionId.Substring(2).Trim(Empty) : "";
                if (suffix.Length > 0)
                {
                    symbol += "_" + (AirRoleLabels.TryGetValue(suffix, out var role) ? role : suffix);
                }

                return;
            }

            if (Symbols.TryGetValue(functionId, out var entry))
            {
                part = Single(SymbolParts, CharAt(entry.GeneratorCode, 0));
                domain = Single(SymbolDomains, CharAt(entry.GeneratorCode, 2));
                symbol = entry.Label;
                return;
            }

            // Ground units/equipment: derive Part from the leading char, Domain is already
            // shown by the Battle Dimension field so leave it empty.
            if (GroundFunctions.TryGetValue(functionId, out var groundLabel))
            {
                part = Single(SymbolParts, CharAt(functionId, 0));
                domain = Empty.ToString();
                symbol = groundLabel;
                return;
            }

            // Unknown / custom function id: surface the raw code without inventing fields.
            part = Empty.ToString();
            domain = Empty.ToString();
            symbol = functionId;
        }

        private static char CharAt(string value, int index)
        {
            return index >= 0 && index < value.Length ? value[index] : Empty;
        }

        private static string Substring(string value, int start, int length)
        {
            if (start >= value.Length)
            {
                return "";
            }

            var available = System.Math.Min(length, value.Length - start);
            return value.Substring(start, available);
        }

        private static string Single(Dictionary<char, string> map, char value)
        {
            if (value == Empty)
            {
                return Empty.ToString();
            }

            return map.TryGetValue(value, out var label) ? label : value.ToString();
        }
    }
}
