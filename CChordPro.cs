﻿using DocumentFormat.OpenXml.Drawing;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using static ChordSheetConverter.CChordSheetLine;
using static ChordSheetConverter.CScales;

namespace ChordSheetConverter
{
    public partial class CChordPro : CBasicConverter    {

        // property / tag
        public static readonly Dictionary<string, string> chordProMapTags = new()
        {
    { "Title", "title" },
    { "SubTitle", "subTitle" },
    { "Composer", "composer" },
    { "Lyricist", "lyricist" },
    { "Copyright", "copyright" },
    { "Album", "album" },
    { "Year", "year" },
    { "Key", "key" },
    { "Time", "time" },
    { "Tempo", "tempo" },
    { "Duration", "duration" },
    { "Capo", "capo" }
};

        public override Dictionary<string, string> PropertyMapTags { get; } = chordProMapTags;

        public string GetFirstValueFromSecondIn_propertyMapTags(string secondValue)
        {
            foreach (var kvp in PropertyMapTags)
            {
                if (kvp.Value == secondValue)
                {
                    return kvp.Key; // Return the first value (property name)
                }
            }
            return ""; // Return null if no match is found
        }

        // Method to return the tags with their content as a formatted string, only for non-empty properties
        public string GetChordProTags()
        {
            // Create a StringBuilder to accumulate the tag strings
            StringBuilder tags = new();

            // Get the properties listed in propertyMapTags (first value of the dictionary)
            var propertiesToExtract = PropertyMapTags.Select(kvp => kvp.Key).ToHashSet();

            // Get all the properties of this class
            PropertyInfo[] properties = this.GetType().GetProperties();

            foreach (var property in properties)
            {
                // Check if the property name exists in propertyMapTags
                if (propertiesToExtract.Contains(property.Name))
                {
                    // Get the value of the property
                    var value = property.GetValue(this)?.ToString();

                    // Only add the tag if the value is not null or empty
                    if (!string.IsNullOrEmpty(value))
                    {
                        // Format and append the tag to the StringBuilder
                        tags.AppendLine($"{{{property.Name.ToLower()}: {value}}}");
                    }
                }
            }

            // Return the accumulated tags as a string
            return tags.ToString();
        }

        public override List<CChordSheetLine> Analyze(string text)
        {
            return Analyze(StringToLines(text));
        }

        enum EnTextType
        {
            Undefined,
            Chorus,
            Verse,
            Bridge
        }

        public override List<CChordSheetLine> Analyze(string[] lines)
        {
            List<CChordSheetLine> chordSheetLines = [];
            string lastSectionStart = "";
            EnLineType ltypeText = EnLineType.Unknown;
            EnLineType ltypeChorus = EnLineType.Unknown;


            // Process each line
            foreach (string line in lines)
            {
                if (line.Contains('{') && line.Contains(':') && line.Contains('}'))
                {
                    (string tagName, string tagValue) = GetTags(line);
                    string propertyName = GetFirstValueFromSecondIn_propertyMapTags(tagName);
                    if (propertyName != "")
                    {
                        // It is a property
                        SetPropertyByName(propertyName, tagValue);
                        chordSheetLines.Add(new CChordSheetLine(EnLineType.xmlElement, line));
                        continue;
                    }
                    //Any other tag
                    if (line.Contains("{start_of_", StringComparison.CurrentCultureIgnoreCase))
                    {
                        lastSectionStart = tagValue;
                    }
                    else if (line.Contains("{comment", StringComparison.CurrentCultureIgnoreCase))
                    {
                        chordSheetLines.Add(new CChordSheetLine(EnLineType.CommentLine, tagValue));
                        continue;
                    }
                    else
                    {
                        chordSheetLines.Add(new CChordSheetLine(EnLineType.xmlElement, line));
                        continue;
                    }
                }
                //Command
                if (line.Contains('{') && line.Contains('}'))
                {
                    var sectionMappings = new List<(string[] StartKeywords, string SectionName, EnLineType TextType, EnLineType ChordType)>
                    {
                        (new[] { "{start_of_verse", "{sov" }, "Verse", EnLineType.TextLineVerse, EnLineType.ChordLineVerse),
                        (new[] { "{start_of_chorus", "{soc" }, "Chorus", EnLineType.TextLineChorus, EnLineType.ChordLineChorus),
                        (new[] { "{start_of_bridge", "{sob" }, "Bridge", EnLineType.TextLineBridge, EnLineType.ChordLineBridge)
                    };

                    bool isStartOfSection = false;

                    foreach (var (keywords, sectionName, textType, chordType) in sectionMappings)
                    {
                        if (keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                        {
                            ltypeText = textType;
                            ltypeChorus = chordType;

                            if (string.IsNullOrEmpty(lastSectionStart))
                            {
                                lastSectionStart = sectionName;
                            }

                            chordSheetLines.Add(new CChordSheetLine(EnLineType.SectionBegin, lastSectionStart));
                            isStartOfSection = true;
                            break;
                        }
                    }

                    if (!isStartOfSection &&
                        (line.Contains("{end_of_", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("{eo", StringComparison.OrdinalIgnoreCase)))
                    {
                        chordSheetLines.Add(new CChordSheetLine(EnLineType.SectionEnd, "End " + lastSectionStart));
                        lastSectionStart = "";
                        ltypeText = EnLineType.TextLine;
                        ltypeChorus = EnLineType.ChordLineVerse;
                    }
                }

                // Check if the line contains chords
                if (line.Contains('[') && line.Contains(']'))
                {
                    // Extract the chords and lyrics
                    (CChordCollection chords, string textLine) = ExtractChords(line);   // Get the chord and text line
                    string chordLine = chords.GetWellSpacedChordLine();

                    // Add ChordLine and TextLine separately
                    chordSheetLines.Add(new CChordSheetLine(ltypeChorus, chordLine));
                    if (!string.IsNullOrEmpty(textLine))
                        chordSheetLines.Add(new CChordSheetLine(ltypeText, textLine));
                    continue;
                }
                if (line == "")
                {
                    chordSheetLines.Add(new CChordSheetLine(EnLineType.EmptyLine, ""));
                    continue;
                }
                chordSheetLines.Add(new CChordSheetLine(EnLineType.Unknown, line));
            }
            return chordSheetLines;
        }

        [GeneratedRegex(@"\[([A-G][#b]?m?\d*)\]")]
        private static partial Regex RegexExtractChords();



        // Helper method to extract chords from a line
        private static (CChordCollection chords, string lyrics) ExtractChords(string line)
        {
            var chords = new CChordCollection();
            var lyricsBuilder = new StringBuilder();
            var chordBuilder = new StringBuilder();
            int pos = 0;
            bool inChord = false;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '[')
                {
                    // Start of chord
                    inChord = true;
                    chordBuilder.Clear(); // Reset the chord builder for a new chord
                }
                else if (line[i] == ']')
                {
                    // End of chord
                    if (inChord && chordBuilder.Length > 0)
                    {
                        chords.AddChord(new CChord(chordBuilder.ToString(), pos));
                    }
                    inChord = false;
                }
                else if (inChord)
                {
                    // Inside a chord, build the chord text
                    chordBuilder.Append(line[i]);
                }
                else
                {
                    // Outside of chord, build lyrics and increment position
                    lyricsBuilder.Append(line[i]);
                    pos++;
                }
            }

            return (chords, lyricsBuilder.ToString());
        }


        [GeneratedRegex(@"\[([A-G][#b]?m?\d*)\]")]
        private static partial Regex RegexRemoveChords();

        // Helper method to remove chords from a line
        private static string RemoveChords(string line)
        {
            return RegexRemoveChords().Replace(line, "").Trim();
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex RegexBuild();

        public override string Build(List<CChordSheetLine> chordSheetLines)
        {
            List<string> ns = []; //new song
            bool inChorus = false;
            bool inVerse = false;
            bool inBridge = false;

            void endsections()
            {
                if (inChorus)
                {
                    //Close Chorus section
                    ns.Add("{eoc}");
                    inChorus = false;
                }
                if (inVerse)
                {
                    ns.Add("{eov}");
                    inVerse = false;
                }
                if (inBridge)
                {
                    ns.Add("{eob}");
                    inVerse = false;
                }
            }


            ns.Add("{ns}");
            ns.Add(GetChordProTags());

            int idx = 0;
            while (idx < chordSheetLines.Count)
            {
                EnLineType thisLineType = chordSheetLines[idx].LineType;

                if (idx < chordSheetLines.Count - 1)
                {
                    if (((thisLineType == EnLineType.ChordLineVerse) ||
                        (thisLineType == EnLineType.ChordLineBridge) ||
                        (thisLineType == EnLineType.ChordLineChorus))
                        &&
                        ((chordSheetLines[idx + 1].LineType == EnLineType.TextLine) ||
                        (chordSheetLines[idx + 1].LineType == EnLineType.TextLineVerse) ||
                        (chordSheetLines[idx + 1].LineType == EnLineType.TextLineChorus) ||
                        (chordSheetLines[idx + 1].LineType == EnLineType.TextLineBridge))
                        )
                    {
                        //Combine lines
                        //It is a chord line + lyrics combination
                        string chords = chordSheetLines[idx].Line;

                        string lyric = chordSheetLines[idx + 1].Line.Trim();
                        string chord = "";
                        int offset = 0;
                        for (int i = 0; i < chords.Length; i++)
                        {
                            if (chords[i] != ' ')
                            {
                                chord += chords[i];
                            }
                            if (chords[i] == ' ' || i == chords.Length - 1)
                            {
                                if (chord != "" && chord != " " && chord != CChordSheetLine.nonBreakingSpace)
                                {
                                    int j = i - chord.Length;
                                    if (j < 0) j = 0;       //insert chord @ beginning of the line
                                    if (i - chord.Length + offset < lyric.Length)
                                        lyric = lyric[..(j + offset)] + "[" + chord.Trim() + "]" + lyric[(j + offset)..];
                                    else
                                        lyric += chord;
                                    offset += chord.Length + 2;
                                    chord = "";
                                }
                            }
                        }

                        ns.Add(lyric);
                        idx += 2;
                        continue;
                    }
                }


                //Isolated Line
                string line = chordSheetLines[idx].Line.Trim();
                if (thisLineType == EnLineType.ChordLineVerse)
                {
                    //Chorus without following text line = isolated chord line

                    line = RegexBuild().Replace(line, " "); // Use Regex to replace multiple spaces with a single space
                    string[] ss = line.Split(' ');
                    string allchords = "";
                    for (int k = 0; k < ss.Length; k++)
                    {
                        if (ss[k] != " " && ss[k] != CChordSheetLine.nonBreakingSpace)
                        {
                            //Surround Chords with []
                            allchords += '[' + ss[k] + "] ";
                        }
                    }
                    ns.Add(allchords);
                }
                else if (thisLineType == EnLineType.SectionBegin)
                {
                    if (line.Contains('V'))
                    {
                        endsections();
                        ns.Add($"{{start_of_verse: label=\"{line}\"}}");
                        inVerse = true;
                    }
                    if (line.Contains('C'))
                    {
                        endsections();
                        ns.Add($"{{start_of_chorus: label=\"{line}\"}}");
                        inChorus = true;
                    }

                    if (line.Contains('B'))
                    {
                        endsections();
                        ns.Add($"{{start_of_bridge: label=\"{line}\"}}");
                        inBridge = true;
                    }
                }
                else if (thisLineType == EnLineType.CommentLine)
                {
                    //Comment
                    if (line.Length > 0)
                    {
                        AddMakeCommentString(ref ns, line);
                    }
                }
                else if (thisLineType == EnLineType.ColumnBreak)
                {
                    //New Column
                    ns.Add("{column_break}");
                }
                else if (thisLineType == EnLineType.PageBreak)
                {
                    //New Page
                    ns.Add("{new_page}");
                }
                else if (thisLineType == EnLineType.EmptyLine)
                {
                    //Line break = empty line
                    endsections();
                    ns.Add("");
                }
                else
                {
                    AddMakeCommentString(ref ns, line);
                }
                idx++;

            }
            return LinesToString([.. ns]);

        }

        private static void AddMakeCommentString(ref List<string> destination, string comment)
        {
            if (comment != "")
            {
                destination.Add("{comment:" + comment + "}");
            }
        }

        //Transpose Letter or Nashville
        public override string[] Transpose(string[] linesIn, TranspositionParameters? parameters = null, int? steps = null)
        {
            List<string> transposedLines = [];

            foreach (string line in linesIn)
            {
                if (line.Contains('[') && line.Contains(']'))
                {
                    //if (!string.IsNullOrEmpty(""))
                    //    transposedLines.Add(TransposeChordProLineNashville(line, sourceKey, sourceScaleType, targetKey, targetScaleType));
                    //else
                        transposedLines.Add(TransposeChordProLine(line, parameters));
                }
                else
                {
                    transposedLines.Add(line);  // No chords in the line, add it unchanged
                }
            }

            return [.. transposedLines];
        }

        //One letter line
        private string TransposeChordProLine(string lineIn, TranspositionParameters parameters)
        {
            // Pattern to match chords within square brackets, e.g. [C], [F#], [G#m]
            string chordPattern = @"\[([A-G][#b]?m?(maj|sus|dim|aug)?[0-9]?(add[0-9])?)\]";

            return Regex.Replace(lineIn, chordPattern, match =>
            {
                // Extract the chord inside the brackets (without the brackets)
                string chord = match.Groups[1].Value;

                // Transpose the chord
                string transposedChord = Transpose(chord, parameters);

            // Return the transposed chord wrapped in brackets
            return $"[{transposedChord}]";
            });
        }

        //One Nashville line
        private string TransposeChordProLineNashville(string line, int steps, string key, ScaleType scaleType)
        {
            // Pattern to match chords within square brackets, e.g., [1], [4m], [5maj7]
            string chordPattern = @"\[([1-7](m|maj|sus|dim|aug|7|add[0-9]*)?)\]";

            // Replace each Nashville chord in the line
            return Regex.Replace(line, chordPattern, match =>
            {
                // Extract the Nashville chord (without brackets)
                string nashvilleChord = match.Groups[1].Value;

                // Transpose the Nashville chord
                string transposedChord = CScales.TransposeNashville(nashvilleChord, steps);

                // Return the transposed Nashville chord wrapped in brackets
                return $"[{transposedChord}]";
            });
        }


        public static string ConvertChordProToNashville(string textIn, string key, ScaleType scaleType = ScaleType.Major)
        {
            return LinesToString(ConvertChordProToNashville(StringToLines(textIn), key,scaleType));
        }
        public static string[] ConvertChordProToNashville(string[] linesIn, string key, ScaleType scaleType = ScaleType.Major)
        {
            List<string> nashvilleLines = [];

            foreach (string line in linesIn)
            {
                if (line.Contains('[') && line.Contains(']'))
                {
                    nashvilleLines.Add(ConvertChordProLineToNashville(line, key, scaleType));
                }
                else
                {
                    nashvilleLines.Add(line);  // No chords in the line, add it unchanged
                }
            }

            return [.. nashvilleLines];
        }

        private static string ConvertChordProLineToNashville(string line, string key, ScaleType scaleType)
        {
            // Pattern to match letter chords within square brackets, e.g., [C], [G#m7]
            string chordPattern = @"\[([A-G][#b]?m?(maj|sus|dim|aug)?[0-9]?(add[0-9])?)\]";

            // Replace each letter chord in the line
            return Regex.Replace(line, chordPattern, match =>
            {
                // Extract the letter chord (without brackets)
                string letterChord = match.Groups[1].Value;

                // Convert the letter chord to Nashville notation
                string nashvilleChord = CScales.ConvertChordToNashville(letterChord, key, scaleType);

                // Return the Nashville chord wrapped in brackets
                return $"[{nashvilleChord}]";
            });
        }
    }
}

