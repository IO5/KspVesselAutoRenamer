using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using static KSP.UI.Screens.Mapview.MapNode;

namespace VesselAutoRenamer
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.SPACECENTER, GameScenes.EDITOR, GameScenes.FLIGHT })]
    public class VesselAutoRenamer : ScenarioModule
    {
        public Dictionary<uint, string> nameHistory = new Dictionary<uint, string>{};

        public static Dictionary<char, NamingScheme> Schemes = new Dictionary<char, NamingScheme> {
            { 'd', new Scheme.Decimal{ } },
            { 'x', new Scheme.Hex{ letterCase=Case.Lower } },
            { 'X', new Scheme.Hex{ letterCase=Case.Upper } },
            { 'b', new Scheme.Binary{ padded=false } },
            { 'B', new Scheme.Binary{ padded=true } },
            { 'r', new Scheme.Roman{ letterCase=Case.Lower } },
            { 'R', new Scheme.Roman{ letterCase=Case.Upper } },
            { 'a', new Scheme.LatinAlphabet{ letterCase=Case.Lower } },
            { 'A', new Scheme.LatinAlphabet{ letterCase=Case.Upper } },
            { 'g', new Scheme.GreekAlphabet{ letterCase=Case.Lower } },
            { 'G', new Scheme.GreekAlphabet{ letterCase=Case.Upper } },
            { 'w', new Scheme.GreekAlphabetAsWords{ letterCase=Case.Lower } },
            { 'W', new Scheme.GreekAlphabetAsWords{ letterCase=Case.Upper } },
            { 'p', new Scheme.NatoPhoneticAlphabet{ letterCase=Case.Lower } },
            { 'P', new Scheme.NatoPhoneticAlphabet{ letterCase=Case.Upper } },
            { 'l', new Scheme.LegacyIcaoPhoneticAlphabet{ letterCase=Case.Lower } },
            { 'L', new Scheme.LegacyIcaoPhoneticAlphabet{ letterCase=Case.Upper } },
        };

        public override void OnLoad(ConfigNode node)
        {
            Debug.Log("[VesselAutoRenamer] OnLoad", this);
            base.OnLoad(node);

            foreach (var vessel in node.GetNodes("VESSEL"))
            {
                var name = vessel.GetValue("name");
                uint pid = 0;
                if (name == null || !vessel.TryGetValue("pid", ref pid) || pid == 0)
                {
                    Debug.LogError("[VesselAutoRenamer] Malformed entry in the savefile", this);
                    continue;
                }

                nameHistory[pid] = name;
            }

            if (nameHistory.Count == 0)
                Debug.Log("[VesselAutoRenamer] No name history found", this);
        }

        public override void OnSave(ConfigNode node)
        {
            Debug.Log("[VesselAutoRenamer] OnSave", this);
            base.OnSave(node);

            foreach ((uint pid, string name) in nameHistory.Select(x => (x.Key, x.Value)))
            {
                var vessel = node.AddNode("VESSEL");
                vessel.AddValue("pid", pid);
                vessel.AddValue("name", name);
            }
        }

        public override void OnAwake()
        {
            GameEvents.OnFlightGlobalsReady.Add(OnFlightGlobalsReady);
            GameEvents.OnVesselRollout.Add(OnVesselRollout);
            GameEvents.onVesselRename.Add(OnVesselRename);
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onPartVesselNamingChanged.Add(HandleVesselNamingSymmetry);
                GameEvents.onEditorPartPlaced.Add(HandleVesselNamingSymmetry);
            }
            else
            {
                GameEvents.onVesselsUndocking.Add(OnDecouple);
                GameEvents.onPartDeCoupleNewVesselComplete.Add(OnDecouple);
            }

#if DEBUG
            RunTests();
#endif
        }

        private void OnDestroy()
        {
            GameEvents.OnFlightGlobalsReady.Remove(OnFlightGlobalsReady);
            GameEvents.OnVesselRollout.Remove(OnVesselRollout);
            GameEvents.onVesselRename.Remove(OnVesselRename);
            if (HighLogic.LoadedSceneIsEditor)
            {
                GameEvents.onPartVesselNamingChanged.Remove(HandleVesselNamingSymmetry);
                GameEvents.onEditorPartPlaced.Remove(HandleVesselNamingSymmetry);
            }
            else
            {
                GameEvents.onVesselsUndocking.Remove(OnDecouple);
                GameEvents.onPartDeCoupleNewVesselComplete.Remove(OnDecouple);
            }
        }

        protected void OnFlightGlobalsReady(bool ready)
        {
            if (ready && nameHistory.Count == 0)
                PopulateHistory();
        }

        protected void OnVesselRollout(ShipConstruct ship)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;

            string ogName = vessel.vesselName;
            string newName = TryFormatName(ogName.Trim());

            if (newName == ogName)
            {
                foreach (var line in ship.shipDescription.Split('\n'))
                {
                    var bracketStart = line.IndexOf('[');
                    if (bracketStart == -1)
                        continue;

                    var bracketEnd = line.IndexOf(']', bracketStart);
                    if (bracketEnd == -1)
                        continue;

                    newName = line.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim();
                    newName = TryFormatName(newName);
                }
            }

            if (newName != ogName)
            {
                UpdateVesselNaming(vessel, newName);
                vessel.vesselName = newName;
                GameEvents.onVesselRename.Fire(new GameEvents.HostedFromToAction<Vessel, string>(vessel, ogName, newName));
            }

            UpdateNameHistory(vessel);
        }

        protected void OnVesselRename(GameEvents.HostedFromToAction<Vessel, string> eventData)
        {
            UpdateNameHistory(eventData.host);
        }

        protected void OnDecouple(Vessel a, Vessel b)
        {
            var aNewName = TryFormatName(a.vesselName.Trim());
            var bNewName = TryFormatName(b.vesselName.Trim());

            UpdateVesselNaming(a, aNewName);
            UpdateVesselNaming(b, bNewName);

            a.vesselName = aNewName;
            b.vesselName = bNewName;

            UpdateNameHistory(a);
            UpdateNameHistory(b);
        }

        protected void HandleVesselNamingSymmetry(Part part)
        {
            foreach (var child in part.children)
                HandleVesselNamingSymmetry(child);

            if (string.IsNullOrEmpty(part?.vesselNaming?.vesselName))
                return;

            foreach (var counterpart in part.symmetryCounterparts)
            {
                if (string.IsNullOrEmpty(counterpart?.vesselNaming?.vesselName))
                {
                    counterpart.vesselNaming = new VesselNaming();
                    counterpart.vesselNaming.vesselName = part.vesselNaming.vesselName;
                    counterpart.vesselNaming.vesselType = part.vesselNaming.vesselType;
                    counterpart.vesselNaming.namingPriority = part.vesselNaming.namingPriority;

                    RefreshPartVesselNamingDisplay(counterpart);
                }
            }
        }

        public void UpdateNameHistory(Vessel vessel)
        {
            if (vessel == null)
                return;

            nameHistory.Remove(vessel.persistentId);

            switch (vessel.vesselType)
            {
                case VesselType.EVA:
                case VesselType.DeployedScienceController:
                case VesselType.DeployedSciencePart:
                case VesselType.DeployedGroundPart:
                case VesselType.Debris:
                case VesselType.DroppedPart:
                case VesselType.Flag:
                case VesselType.SpaceObject:
                    return;
            }

            if (vessel.DiscoveryInfo?.Level != DiscoveryLevels.Owned)
                return;

            if (vessel.GetDisplayName() == "Untitled Space Craft" || vessel.GetDisplayName() == Localizer.Format("#autoLOC_900530"))
                return;

            if (vessel.persistentId == 0)
                return;

            nameHistory[vessel.persistentId] = vessel.GetDisplayName();
        }

        static protected void UpdateVesselNaming(Vessel vessel, string newName)
        {
            if (vessel.vesselName == newName)
                return;

            foreach (var part in vessel.parts.Where(p => p?.vesselNaming?.vesselName == vessel.vesselName))
            {
                part.vesselNaming.vesselName = newName;
                GameEvents.onPartVesselNamingChanged.Fire(part);

                RefreshPartVesselNamingDisplay(part);
            }
        }

        static internal MethodInfo dynMethod = typeof(Part).GetMethod("RefreshVesselNamingPAWDisplay", BindingFlags.NonPublic | BindingFlags.Instance);

        static protected void RefreshPartVesselNamingDisplay(Part part)
        {
            dynMethod.Invoke(part, new object[] {});
        }

        protected void PopulateHistory()
        {
            nameHistory = new Dictionary<uint, string>{};

            foreach (var vessel in FlightGlobals.Vessels)
                UpdateNameHistory(vessel);
        }

        protected string TryFormatName(string template)
        {
            var prefix = new StringBuilder();
            var suffix = template;
            var idx = suffix.IndexOf('%');

            if (idx == -1 || idx == suffix.Length - 1)
                return template;

            NamingScheme scheme = null;

            do
            {
                prefix.Append(suffix.Substring(0, idx));
                char c = suffix[idx + 1];
                suffix = suffix.Substring(idx + 2);

                if (c == '%')
                {
                    prefix.Append('%');
                }
                else
                {
                    scheme = GetScheme(c, ref suffix);
                    if (scheme != null)
                    {
                        suffix = suffix.Replace("%%", "%");
                        return ApplyScheme(scheme, prefix.ToString(), suffix);
                    }
                    prefix.Append('%');
                    prefix.Append(c);
                }

                idx = suffix.IndexOf('%');
            }
            while (idx != -1 && idx < suffix.Length - 1);

            return prefix + suffix;
        }
        protected string ApplyScheme(NamingScheme scheme, string prefix, string suffix)
        {
            var names = nameHistory.Select(p => p.Value);

            var matchingNames = names;
            if (prefix.Length > 0)
                matchingNames = matchingNames.Where(n => n.StartsWith(prefix));
            if (suffix.Length > 0)
                matchingNames = matchingNames.Where(n => n.EndsWith(suffix));

            short maxOrd = 0;

            foreach (var name in matchingNames)
            {
                var reminder = name.Substring(prefix.Length);
                reminder = reminder.Substring(0, reminder.Length - suffix.Length);

                if (reminder.Length == 0)
                {
                    if (suffix.Length == 0)
                        maxOrd = Math.Max(maxOrd, (short)1);
                    continue;
                }

                if (scheme.TryConvertToNumber(reminder, out var ord))
                    maxOrd = Math.Max(maxOrd, ord);
            }

            if (maxOrd == 0)
            {
                if (suffix.Length == 0 && names.Any(n => n == prefix.Trim()))
                    maxOrd = 1;
            }

            return prefix + scheme.GetNthName(++maxOrd) + suffix;
        }

        protected static NamingScheme GetScheme(char c, ref string extra)
        {
            NamingScheme scheme;

            if (c != '0')
                return Schemes.TryGetValue(c, out scheme) ? scheme : null;

            int widthLen = 0;
            string extraCopy = extra;

            while (extraCopy.Length > 0)
            {
                c = extraCopy[0];
                extraCopy = extraCopy.Substring(1);

                if (!char.IsNumber(c))
                    break;

                ++widthLen;
            }

            if (widthLen == 0)
                return null;

            if (Int16.TryParse(extra.Substring(0, widthLen), out var width))
            {
                if (Schemes.TryGetValue(c, out scheme))
                {
                    var variableWidthScheme = scheme as Scheme.VariableWidthScheme;
                    if (variableWidthScheme != null)
                    {
                        var clone = variableWidthScheme.Clone();
                        clone.width = width;
                        extra = extraCopy;
                        return clone;
                    }
                }
            }

            return null;
        }

#if DEBUG
        private void RunTests()
        {
            nameHistory = new Dictionary<uint, string>
            {
                { 1, "Foo" },
                { 2, "NumA 1" },
                { 3, "NumB 1" },
                { 4, "NumB 2" },
                { 5, "NumC 0" },
                { 6, "NumD 0002" },
                { 7, "NumE 2137" },
                { 8, "23 NumF" },
                { 9, "42" },
                { 10, "2 NumG 3" },
                { 11, "NumH 5X" },
                { 12, "NumI 023" },
                { 13, "RomA I" },
                { 14, "RomB III" },
                { 15, "RomC iv" },
                { 16, "RomD XX" },
                { 17, "RomE XXX" },
                { 18, "RomF DCCCXCIV" }, // 894
                { 19, "HexA AA" },
                { 20, "HexB 01f" },
                { 21, "HexC 0f0" },
                { 22, "LatA A" },
                { 23, "LatB-f" },
                { 24, "LatC-5G" },
                { 25, "GreA Α" },
                { 26, "GreB λ" },
                { 27, "GreC Alpha" },
                { 28, "GreD zeta" },
                { 29, "es%cape 5%" },
                { 30, "Delta Force" },
                { 31, "Excel1 Z" },
                { 32, "Excel2 AA" },
                { 33, "Excel3 ZZ" },
                { 34, "Excel4 aCbX" },
                { 35, "Excel5 xyZ" },
            };

            Action<string, string> test = (format, expected) =>
            {
                var value = TryFormatName(format);
                if (value != expected)
                    Debug.LogError($"Assertion Failed: \"{value}\" != \"{expected}\"");
            };

            test("", "");
            test("some", "some");
            test("percent%", "percent%");
            test("escape%%", "escape%");
            test("%%escape", "%escape");
            test("es%%ca%%pe", "es%ca%pe");
            test("Foo %d", "Foo 2");
            test("Foo%d", "Foo2");
            test("Bar %d", "Bar 1");
            test("NumA %d", "NumA 2");
            test("NumB %d", "NumB 3");
            test("NumC %d", "NumC 1");
            test("NumD %d", "NumD 3");
            test("NumD %05d", "NumD 00003");
            test("NumE %d", "NumE 2138");
            test("%d NumF", "24 NumF");
            test("%d", "43");
            test("%d NumG 3", "3 NumG 3");
            test("2 NumG %d", "2 NumG 4");
            test("%d NumG %d", "1 NumG %d");
            test("NumH %dX", "NumH 6X");
            test("NumI %d", "NumI 24");
            test("NumI %01d", "NumI 24");
            test("NumI %03d", "NumI 024");
            test("NumI %0 d", "NumI %0 d");
            test("RomA %R", "RomA II");
            test("RomB %r", "RomB iv");
            test("RomC %R", "RomC V");
            test("RomD %R", "RomD XXI");
            test("RomE %RX", "RomE XXIX");
            test("RomF %R", "RomF DCCCXCV");
            test("HexA %x", "HexA ab");
            test("HexB %X", "HexB 20");
            test("HexC %03X", "HexC 0F1");
            test("LatA %a", "LatA b");
            test("LatB-%A", "LatB-G");
            test("LatC-5%A", "LatC-5H");
            test("GreA %g", "GreA β");
            test("GreB %G", "GreB Μ");
            test("GreC %W", "GreC Beta");
            test("GreD %w", "GreD eta");
            test("es%%cape %d%%", "es%cape 6%");
            test("%p", "alpha");
            test("%P Force", "Echo Force");
            test("Excel1 %a", "Excel1 aa");
            test("Excel2 %A", "Excel2 AB");
            test("Excel3 %A", "Excel3 AAA");
            test("Excel4 %A", "Excel4 ACBY");
            test("Excel5 %a", "Excel5 xza");

            nameHistory.Clear();
        }
#endif
    }
}
