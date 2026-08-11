using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FF34Manip
{
    // The manip list is read from manips.txt next to the exe so users can 
    // add their own. The file is created from the defaults below the first time the app runs.
    public class ManipList
    {
        public const string FileName = "manips.txt";

        public List<Manip> FF3 { get; } = new List<Manip>();
        public List<Manip> FF4 { get; } = new List<Manip>();
        // One entry per line that couldn't be read - MainWindow shows these to the user
        public List<string> Errors { get; } = new List<string>();
        // True if the file itself couldn't be opened, so the built-in manips are in use
        public bool FileUnavailable { get; private set; }

        // BaseDirectory rather than Assembly.Location, which is empty in a single-file publish
        public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

        public ManipList()
        {
            Parse(LoadLines());
        }

        // Reads manips.txt, creating it from the defaults if it isn't there yet.
        // Falls back to the built-in defaults so the app still works if the file can't be used.
        private string[] LoadLines()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    File.WriteAllLines(FilePath, DefaultLines);
                }
                return File.ReadAllLines(FilePath);
            }
            catch (Exception e)
            {
                FileUnavailable = true;
                Errors.Add($"Could not open {FileName}, so the built-in manips are being used.\n\nWindows said: {e.Message}");
                return DefaultLines;
            }
        }

        private void Parse(string[] lines)
        {
            List<Manip> section = FF3;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // Blank lines and comments
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                // Section header - decides which tab the following manips appear on
                if (line[0] == '[')
                {
                    if (string.Equals(line, "[FF3]", StringComparison.OrdinalIgnoreCase))
                    {
                        section = FF3;
                    }
                    else if (string.Equals(line, "[FF4]", StringComparison.OrdinalIgnoreCase))
                    {
                        section = FF4;
                    }
                    else
                    {
                        AddError(i, line, "This should say [FF3] or [FF4].");
                    }
                    continue;
                }

                if (TryParseManip(line, out Manip manip, out string error))
                {
                    section.Add(manip);
                }
                else
                {
                    AddError(i, line, error);
                }
            }
        }

        // Shows the user the line that went wrong and what to do about it
        private void AddError(int index, string line, string explanation)
        {
            Errors.Add($"Line {index + 1} says:\n    {line}\n{explanation}");
        }

        // Name = TimeZone, Day, Month, Year, Hour, Minute, Second
        private static bool TryParseManip(string line, out Manip manip, out string error)
        {
            manip = default;

            int split = line.IndexOf('=');
            if (split < 0)
            {
                error = "There is no = sign on this line.";
                return false;
            }

            string name = line.Substring(0, split).Trim();
            if (name.Length == 0)
            {
                error = "There is no name before the = sign.";
                return false;
            }

            string[] values = line.Substring(split + 1).Split(',');
            if (values.Length != 7)
            {
                string counted = values.Length == 1 ? "only 1" : $"{values.Length}";
                error = $"There should be 7 things after the = sign, but this line has {counted}.";
                return false;
            }

            string timeZone = ManipController.ResolveTimeZone(values[0]);
            if (!IsKnownTimeZone(timeZone))
            {
                error = $"'{values[0].Trim()}' is not a time zone. Use CEST, GMT, ET, UTC or JST.";
                return false;
            }

            // Day, Month, Year, Hour, Minute, Second - kept as typed so 2 and 4 digit years both work
            short[] numbers = new short[6];
            string[] labels = { "day", "month", "year", "hour", "minute", "second" };
            for (int i = 0; i < numbers.Length; i++)
            {
                string value = values[i + 1].Trim();
                if (!short.TryParse(value, out numbers[i]))
                {
                    error = value.Length == 0
                        ? $"The {labels[i]} is missing."
                        : $"The {labels[i]} should be a number, but it says '{value}'.";
                    return false;
                }
            }

            if (!IsRealDateAndTime(numbers, out error))
            {
                return false;
            }

            manip = new Manip(name, timeZone, numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5]);
            error = string.Empty;
            return true;
        }

        // Windows just refuses a date or time it doesn't like without saying why,
        // so catch impossible ones here instead. Values are day, month, year, hour, minute, second.
        private static bool IsRealDateAndTime(short[] numbers, out string error)
        {
            short day = numbers[0], month = numbers[1], year = numbers[2];

            if (!IsBetween(numbers[3], 0, 23, "hour", out error)) return false;
            if (!IsBetween(numbers[4], 0, 59, "minute", out error)) return false;
            if (!IsBetween(numbers[5], 0, 59, "second", out error)) return false;
            if (!IsBetween(month, 1, 12, "month", out error)) return false;

            // A year can be written either way - Windows expands 2 digit ones itself
            bool twoDigit = year >= 0 && year <= 99;
            bool fourDigit = year >= 1601 && year <= 9999;
            if (!twoDigit && !fourDigit)
            {
                error = $"The year should be 2 digits like 25, or 4 digits like 2025, but it says '{year}'.";
                return false;
            }

            if (day < 1)
            {
                error = $"The day should be 1 or more, but it says '{day}'.";
                return false;
            }

            int fullYear = twoDigit ? CultureInfo.CurrentCulture.Calendar.ToFourDigitYear(year) : year;
            int daysInMonth = DateTime.DaysInMonth(fullYear, month);
            if (day > daysInMonth)
            {
                string monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
                error = $"{monthName} {fullYear} has {daysInMonth} days, so day {day} does not exist.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsBetween(short value, int low, int high, string label, out string error)
        {
            if (value >= low && value <= high)
            {
                error = string.Empty;
                return true;
            }
            error = $"The {label} should be between {low} and {high}, but it says '{value}'.";
            return false;
        }

        private static bool IsKnownTimeZone(string id)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string[] DefaultLines => DefaultFileText.Replace("\r\n", "\n").Split('\n');

        // The only copy of the default manips - written out as manips.txt on first run
        private const string DefaultFileText = @"# FF34Manip manip list
# --------------------------------------------------------------------
# Edit the times below, or add your own manips.
# Lines starting with # are ignored.
#
# Format:   Name = TimeZone, Day, Month, Year, Hour, Minute, Second
#
# Name      is the text shown on the button.
# TimeZone  is one of: ET, UTC, JST, GMT, CEST
#           or any full Windows time zone ID (run tzutil /l to list them).
# Year      may be 2 or 4 digits - it is passed to Windows exactly as typed.
# Hour      is on a 24 hour clock, so 0 to 23. Half past 1 in the afternoon is 13, 30, 0.
#
# Manips under [FF3] appear on the FF3 tab, [FF4] on the FF4 tab.
# Restart the app to apply your changes.
# Delete this file and restart the app to restore the defaults.
# --------------------------------------------------------------------

[FF3]
Altar Cave         = GMT,  10, 04, 21, 19, 43, 17
Sealed Cave        = CEST, 02, 05, 24, 09, 48, 08
Dragon's Peak      = CEST, 02, 05, 24, 11, 42, 36
Tozus Tunnel       = CEST, 02, 05, 24, 11, 57, 50
To Tower of Owen   = CEST, 02, 05, 24, 13, 15, 41
Tower of Owen      = CEST, 17, 02, 24, 10, 00, 07
Subterranean Lake  = CEST, 30, 04, 24, 13, 01, 56
Molten Cave        = CEST, 21, 02, 24, 12, 51, 32
Hein's Castle      = CEST, 11, 02, 24, 13, 10, 42
Cave of Tides      = CEST, 02, 05, 24, 18, 14, 42
Amur Sewers        = CEST, 03, 05, 24, 12, 02, 16
Chocobo's Wrath    = CEST, 14, 10, 23, 11, 04, 04
Goldor Manor       = CEST, 05, 05, 24, 10, 21, 39
Garuda             = CEST, 15, 10, 23, 12, 37, 19
Cave of the Circle = CEST, 15, 10, 23, 13, 07, 36
Saronia Catacombs  = CEST, 05, 05, 24, 13, 08, 20
Ancients' Maze     = CEST, 05, 04, 24, 19, 35, 15
Cave of Shadows    = CEST, 23, 01, 24, 12, 07, 14
Shining Curtain    = CEST, 01, 04, 24, 18, 30, 13
Doga's Grotto      = CEST, 16, 10, 23, 10, 50, 57
To Xande           = CEST, 21, 10, 23, 21, 35, 31
World of Darkness  = CEST, 28, 10, 23, 14, 58, 25
Cloud of Darkness  = CEST, 04, 04, 24, 12, 36, 49

[FF4]
New Game           = CEST, 24, 10, 2021, 16, 20, 00
Octomammoth        = CEST, 15, 03, 25, 14, 09, 00
Mysidia/Ordeals    = CEST, 24, 04, 2021, 16, 20, 08
Rainbow Pudding    = CEST, 29, 03, 25, 12, 16, 31
Underworld         = CEST, 29, 05, 25, 19, 24, 00
Lugae              = CEST, 01, 03, 25, 23, 31, 31
Babil/Rubi         = CEST, 24, 04, 2021, 16, 20, 17
Sealed Cave FF4    = CEST, 09, 06, 25, 13, 33, 26
Safe Travel        = CEST, 11, 05, 2021, 16, 45, 00
Dragon One Cycle   = CEST, 25, 05, 25, 11, 55, 22
Pink Tail          = CEST, 24, 04, 2021, 16, 20, 27
";
    }
}
