using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FF34Manip
{
    public partial class MainWindow : Window
    {
        public static string AppVersion => $"Version 2.0 - 2026-08-11";
        public ManipController ManipController = new ManipController();
        public ManipList Manips { get; } = new ManipList();
        public static string systemDateFormat;
        public static short timeOffset = 0;

        public MainWindow()
        {
            InitializeComponent();
            systemDateFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            ReportManipFileErrors();
            InitializeTimeService();
        }

        // Bad lines are skipped rather than fixed, so tell the user which ones were ignored
        private void ReportManipFileErrors()
        {
            if (Manips.Errors.Count == 0)
            {
                return;
            }

            // The file couldn't be opened at all - that message explains itself
            if (Manips.FileUnavailable)
            {
                MessageBox.Show(Manips.Errors[0], ManipList.FileName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string intro = Manips.Errors.Count == 1
                ? $"1 line in {ManipList.FileName} didn't make sense, so it was skipped."
                : $"{Manips.Errors.Count} lines in {ManipList.FileName} didn't make sense, so they were skipped.";

            MessageBox.Show(
                $"{intro}\nEvery other manip still works as normal.\n\n" +
                $"{string.Join("\n\n", Manips.Errors)}\n\n" +
                "That is: name, time zone, day, month, year, hour, minute, second.\n\n",
                ManipList.FileName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // Verify time service is active to enable /resync
        public void InitializeTimeService()
        {
            string args = "start w32time";
            using (Process startTimeService = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "net.exe",
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            })
            {
                startTimeService.Start();
                startTimeService.WaitForExit();
            }
        }

        // Forcing a repaint below lets clicks through, so ignore any that land mid-manip
        private bool manipRunning;

        private void StartManip(object sender, RoutedEventArgs args)
        {
            // Each button is generated from a manips.txt entry, which it carries as its DataContext
            if (manipRunning || args.Source is not Button { DataContext: Manip manip })
            {
                return;
            }

            manipRunning = true;
            try
            {
                ManipController.ExecuteManip(manip);
            }
            finally
            {
                manipRunning = false;
            }
        }

        // The manip loop runs on the UI thread and blocks it, so the countdown would never
        // appear on its own. Setting the text then pumping at Render priority forces it to draw.
        public static void ShowManipStatus(string text)
        {
            if (Application.Current?.MainWindow is MainWindow window)
            {
                window.manipStatus.Text = text;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            }
        }

        // Check for valid positive and negative integers
        private static bool IsNumeric(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            // Allow a single hyphen for negative offsets
            if (text == "-")
            {
                return true;
            }
            if (text[0] == '-' && text.Length > 1)
            {
                return int.TryParse(text.Substring(1), out _);
            }
            return int.TryParse(text, out _);
        }
        
        // Check input is valid before registering or parsing it
        private void Offset_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Parse for valid input
            TextBox textBox = e.Source as TextBox;
            e.Handled = !IsNumeric(textBox.Text + e.Text);
            
            // Assign offset if input is valid
            if (short.TryParse(textBox.Text + e.Text, out short result))
            {
                timeOffset = result;
            }
        }

        private void Offset_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Invalidate spaces
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
            // Prevent pasting non-numeric values
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                string clipboardText = Clipboard.GetText();
                e.Handled = !IsNumeric(clipboardText);
            }
        }
    }
}

