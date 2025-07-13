using System;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Windows;

namespace FF34Manip
{
    public class ManipController
    {
        private string savedTimeZone;
        private string currentTimeZone;
        private string currentDate;
        private bool timeAdjustedForOffset;

        private enum DateFormats
        {
            DDMMYYYY,
            MMDDYYYY,
            YYYYMMDD
        }

        public static class TimeZones
        {
            public const string ET = "Eastern Standard Time";
            public const string UTC = "UTC";
            public const string JST = "Tokyo Standard Time";
            public const string GMT = "GMT Standard Time";
            public const string CEST = "W. Europe Standard Time";
            // Add more as needed - string needs to match output from tzutil.exe /l
        }
        
        private DateFormats GetDateFormat()
        {
            string datePattern = MainWindow.systemDateFormat;
            if (datePattern[0] == 'd')
            {
                return DateFormats.DDMMYYYY;
            }

            if (datePattern[0] == 'M')
            {
                return DateFormats.MMDDYYYY;
            }
            return DateFormats.YYYYMMDD;
        }

        private bool GameRunning()
        {
            return Process.GetProcessesByName("FF3_Win32").Length > 0 || Process.GetProcessesByName("FF4").Length > 0;
        }

        public void ExecuteManip(ManipList.ManipNames name)
        {
            savedTimeZone = TimeZoneInfo.Local.StandardName;

            ManipList manipList = new ManipList();
            timeAdjustedForOffset = false;
            currentDate = "";
            SetDateTime(manipList.GetManipByValue(name));
        }

        private void SetTimeZone(string targetTimeZone)
        {
            string args = "/s \"" + targetTimeZone + "\"";
            using(Process setTimeZoneProcess = new Process
                  {
                      StartInfo = new ProcessStartInfo
                      {
                          FileName = "tzutil.exe",
                          Arguments = args,
                          CreateNoWindow = true,
                          WindowStyle = ProcessWindowStyle.Hidden
                      }
                  })
            {
                setTimeZoneProcess.Start();
                setTimeZoneProcess.WaitForExit();
                setTimeZoneProcess.Close();
            }
            
            currentTimeZone = targetTimeZone;
        }
        
        private void SetDate(Manip targetManip)
        {
            string date = string.Empty;

            switch (GetDateFormat())
            {
                case DateFormats.DDMMYYYY:
                    date = $"{targetManip.Day}-{targetManip.Month}-{targetManip.Year}";
                    break;
                case DateFormats.MMDDYYYY:
                    date = $"{targetManip.Month}-{targetManip.Day}-{targetManip.Year}";
                    break;
                case DateFormats.YYYYMMDD:
                    date = $"{targetManip.Year}-{targetManip.Month}-{targetManip.Day}";
                    break;
            }

            if (date != currentDate)
            {
                using (Process setDateProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C date " + date,
                        CreateNoWindow = true,
                        Verb = "runas",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }}) {
                    setDateProcess.Start();
                    setDateProcess.WaitForExit();
                    setDateProcess.Close();
                    currentDate = date;
                }
            }
        }

        private string ParseTime(ref Manip targetManip)
        {
            // TODO: Adjust for minutes that over/underflow into hour
            // I've left this for now as it's an extremely unlikely scenario (essentially requires exactly x:00:00)
            string time = targetManip.Hour + ":" + targetManip.Minute + ":" + targetManip.Second + ".00";
            if (timeAdjustedForOffset)
                return time;
            
            targetManip.Second += MainWindow.timeOffset;
            if (targetManip.Second < 0)
            {
                int gap = 0 - targetManip.Second;
                targetManip.Minute--;
                targetManip.Second = (short)(60 - gap);
            }

            if (targetManip.Second >= 60)
            {
                int gap = targetManip.Second - 60;
                targetManip.Minute++;
                targetManip.Second = (short)gap;
            }

            return time;
        }

        private void SetAndRevertTime(Manip targetManip)
        {
            SetTime(targetManip);
            // Continuously set time until game is launched or app is unfocused
            try
            {
                while (!GameRunning() && Application.Current.MainWindow.IsActive)
                {
                    SetTime(targetManip);
                }
                // Small buffer to allow the game to launch before reverting
                Stopwatch buffer = Stopwatch.StartNew();
                while (buffer.Elapsed < TimeSpan.FromSeconds(3))
                {
                    SetTime(targetManip);
                }
                RevertTime();
            }
            catch
            {
                // Fix  time on crash, particularly common when a manip is active
                RevertTime();
            }
        }

        private void SetTime(Manip targetManip)
        {
            Thread.Sleep(40);
            using (Process SetTimeProcess = new Process
                   {
                       StartInfo = new ProcessStartInfo
                       {
                           FileName = "cmd.exe",
                           Arguments = $"/C time {ParseTime(ref targetManip)}",
                           CreateNoWindow = true,
                           Verb = "runas",
                           UseShellExecute = true,
                           WindowStyle = ProcessWindowStyle.Hidden
                       }

                   })
            {
                //Console.WriteLine(System.DateTime.Now.Millisecond);
                timeAdjustedForOffset = true;
                SetTimeProcess.Start();
                SetTimeProcess.WaitForExit();
                SetTimeProcess.Close();
            }
        }

        private void SetDateTime(Manip targetManip)
        {
            if (currentTimeZone != targetManip.TimeZone)
            {
                SetTimeZone(targetManip.TimeZone);
            }
            
            SetDate(targetManip);
            SetAndRevertTime(targetManip);
        }
        
        private void RevertTime()
        {
            // Revert Time zone
            using(Process timeZoneRevertProcess = new Process
                  {
                      StartInfo = new ProcessStartInfo
                      {
                          FileName = "tzutil.exe",
                          Arguments = $"/s \"{savedTimeZone}\"",
                          CreateNoWindow = true,
                          WindowStyle = ProcessWindowStyle.Hidden
                      }
                  })
            {
                timeZoneRevertProcess.Start();
                timeZoneRevertProcess.WaitForExit();
                timeZoneRevertProcess.Close();
            }

            TimeZoneInfo.ClearCachedData();
            currentTimeZone = savedTimeZone;
            timeAdjustedForOffset = false;

            using (Process syncProcess = new Process
                   {
                       StartInfo = new ProcessStartInfo
                       {
                           FileName = "w32tm.exe",
                           Arguments = "/resync",
                           CreateNoWindow = true,
                           Verb = "runas",
                           UseShellExecute = true,
                           WindowStyle = ProcessWindowStyle.Hidden
                       }
                   })
            {
                syncProcess.Start();
                syncProcess.WaitForExit();
                syncProcess.Close();
            }
        }
    }
}
