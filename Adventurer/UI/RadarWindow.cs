﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Adventurer.Settings;
using Adventurer.UI.UIComponents;
using Adventurer.Util;
using Zeta.Bot;
using Zeta.Game;

namespace Adventurer.UI
{
    public class MapWindow : Window
    {
        public MapWindow()
        {
            Height = 580;
            Width = 700;
            try
            {
                Content = UILoader.LoadAndTransformXamlFile<UserControl>(Path.Combine(FileUtils.PluginPath, "UI", "MapUI.xaml")); ;
            }
            catch (Exception)
            {
                Content = UILoader.LoadAndTransformXamlFile<UserControl>(Path.Combine(FileUtils.PluginPath2, "UI", "MapUI.xaml")); ;
            }
            Title = "MapUI Powered by Trinity";
        }
        
    }
}
