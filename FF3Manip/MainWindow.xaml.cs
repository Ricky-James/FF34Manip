﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FF3Manip
{
    public partial class MainWindow : Window
    {
        public ManipController ManipController = new ManipController();
        public static string systemDateFormat;
        public static short timeOffset = 0;

        public MainWindow()
        {
            InitializeComponent();
            systemDateFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
        }

        private void StartManip(object sender, RoutedEventArgs args)
        {
            Dictionary<string, ManipList.ManipNames> inputToManipMap = new Dictionary<string, ManipList.ManipNames>
            {
                { "New Game", ManipList.ManipNames.NewGame },
                { "Rainbow Pudding", ManipList.ManipNames.RainbowPudding },
                { "Octomammoth", ManipList.ManipNames.Octomammoth },
                { "Safe Travel", ManipList.ManipNames.SafeTravel },
                { "Pink Tail", ManipList.ManipNames.PinkTail },
                { "Mysidia/Ordeals/Zot", ManipList.ManipNames.MysidiaOrdealsZot },
                { "Lugae/Babil/Rubi", ManipList.ManipNames.LugaeBabilRubi }

            };
            
            string? buttonText = (args.Source as Button).Content.ToString(); // Text on the button
            
            if (inputToManipMap.ContainsKey(buttonText))
            { 
                ManipController.ExecuteManip(inputToManipMap[buttonText]);
            }
            else
            {
                throw new NotSupportedException(sender + " not a recognised or implemented manip");
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

